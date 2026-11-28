#if UNITY_6000_5_OR_NEWER
using System.Collections.Generic;
using Unity.Hierarchy;
using Unity.Hierarchy.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace EmpathyHierarchy
{
    /// <summary>
    /// New (UI Toolkit) Hierarchy only: puts an <see cref="EHNavBar"/> under the search toolbar (Bar mode),
    /// the memo / collapse buttons in the Name header, and catches Alt + Click (bookmark) and
    /// Alt + Right-click (Edit Style) on rows.
    /// </summary>
    [InitializeOnLoad]
    static class EHNavBarManager
    {
        static double _nextPoll;
        static readonly HashSet<VisualElement> HookedRoots = new HashSet<VisualElement>();

        static EHNavBarManager()
        {
            EditorApplication.delayCall += AttachAll;
            EditorApplication.update += Poll;
        }

        // new Hierarchy windows can be opened at any time; checking once a second is cheap
        static void Poll()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < _nextPoll) return;
            _nextPoll = now + 1.0;
            AttachAll();
        }

        static void AttachAll()
        {
            var s = EHSettings.instance;
            bool barMode = s.navBarMode == NavBarMode.Bar;
            bool want = s.enabled && s.navBar && barMode;
            foreach (var w in Resources.FindObjectsOfTypeAll<HierarchyWindow>())
            {
                var root = w.rootVisualElement;
                if (root == null) continue;
                HookRows(root, w);
                EHHeaderButtons.Attach(root, s.enabled);
                var bar = root.Q<EHNavBar>();
                if (!want)
                {
                    bar?.RemoveFromHierarchy();
                    continue;
                }
                if (bar != null) continue;
                var window = w;
                root.Insert(IndexAfterToolbar(root), new EHNavBar(w, () => EHNewHierarchyFilter.For(window), false));
            }
        }

        static int IndexAfterToolbar(VisualElement root)
        {
            for (int i = 0; i < root.childCount; i++)
                if (root[i].ClassListContains("hierarchy-toolbar")) return i + 1;
            return 0;
        }

        // ------------------------------------------------------------------ row clicks

        static bool _suppressMenu;

        static void HookRows(VisualElement root, HierarchyWindow window)
        {
            if (!HookedRoots.Add(root)) return;
            root.RegisterCallback<DetachFromPanelEvent>(_ => HookedRoots.Remove(root));
            // caught at the top of the window, before Unity selects the row or opens its menu
            root.RegisterCallback<PointerDownEvent>(e => OnRowPointerDown(e, window), TrickleDown.TrickleDown);
            root.RegisterCallback<PointerUpEvent>(OnRowPointerUp, TrickleDown.TrickleDown);
            root.RegisterCallback<ContextualMenuPopulateEvent>(OnMenu, TrickleDown.TrickleDown);
        }

        static void OnRowPointerDown(PointerDownEvent e, HierarchyWindow window)
        {
            if (!e.altKey || (e.button != 0 && e.button != 1)) return;
            var s = EHSettings.instance;
            if (!s.enabled) return;
            if (!(e.target is VisualElement ve) || ve.GetFirstAncestorOfType<EHNavBar>() != null) return;

            // the row under the pointer (the target can be the row itself or something inside it)
            var item = ve as HierarchyViewItem ?? ve.GetFirstAncestorOfType<HierarchyViewItem>() ?? ve.Q<HierarchyViewItem>();
            if (item == null) return;
            var go = EHNewHierarchy.GetGameObject(item);
            if (go == null) return;

            if (e.button == 1)
            {
                // Alt + Right-click: Edit Style instead of Unity's context menu
                e.StopImmediatePropagation();
                _suppressMenu = true;
                var targets = Selection.Contains(go) ? Selection.gameObjects : new[] { go };
                if (!Selection.Contains(go)) Selection.activeGameObject = go;
                EditorApplication.delayCall += () => EHStyleWindow.Open(targets);
                return;
            }

            if (!s.navBar) return;
            // Alt + Click on the foldout arrow keeps Unity's "unfold everything below"
            var toggle = item.Toggle;
            if (toggle != null && (ve == toggle || toggle.Contains(ve))) return;
            e.StopImmediatePropagation();
            EHNav.ToggleFromHierarchy(go, msg => window.ShowNotification(new GUIContent(msg), 1.2));
        }

        static void OnRowPointerUp(PointerUpEvent e)
        {
            if (!_suppressMenu || e.button != 1) return;
            e.StopImmediatePropagation();
            // the menu event may follow the pointer-up; let it be swallowed, then reset
            EditorApplication.delayCall += () => _suppressMenu = false;
        }

        static void OnMenu(ContextualMenuPopulateEvent e)
        {
            if (!_suppressMenu) return;
            e.StopImmediatePropagation();
            var items = e.menu.MenuItems();
            for (int i = items.Count - 1; i >= 0; i--) e.menu.RemoveItemAt(i);
        }
    }

    /// <summary>
    /// Bookmark focus for the new Hierarchy: the view is rooted at the object's parent, its siblings are
    /// hidden and it is unfolded; undoing folds back only what was opened by the focus.
    /// </summary>
    class EHNewHierarchyFilter : INavFilter
    {
        static readonly Dictionary<HierarchyWindow, EHNewHierarchyFilter> ByWindow = new Dictionary<HierarchyWindow, EHNewHierarchyFilter>();

        readonly HierarchyWindow _window;
        // siblings we hid so only the bookmarked object (and its children) shows; cleared when the focus changes
        readonly List<HierarchyNode> _hiddenSiblings = new List<HierarchyNode>();
        HierarchyNode _filterParent;
        // folders the focus opened by itself; folded back when it goes away (others stay as they were)
        readonly List<HierarchyNode> _autoExpanded = new List<HierarchyNode>();
        readonly Stack<HierarchyNode> _walk = new Stack<HierarchyNode>();
        GameObject _selected; // object selected by the focus; deselected again when the focus ends

        EHNewHierarchyFilter(HierarchyWindow window) => _window = window;

        internal static EHNewHierarchyFilter For(HierarchyWindow window)
        {
            if (window == null) return null;
            if (!ByWindow.TryGetValue(window, out var f))
            {
                // forget closed windows
                var dead = new List<HierarchyWindow>();
                foreach (var k in ByWindow.Keys) if (k == null) dead.Add(k);
                foreach (var k in dead) ByWindow.Remove(k);
                ByWindow[window] = f = new EHNewHierarchyFilter(window);
            }
            return f;
        }

        static HierarchyWindow _last;

        /// <summary>The focused new Hierarchy, else the last one used, else the first open one.</summary>
        internal static EHNewHierarchyFilter ForLast()
        {
            if (EditorWindow.focusedWindow is HierarchyWindow f) _last = f;
            if (_last == null)
            {
                var all = Resources.FindObjectsOfTypeAll<HierarchyWindow>();
                if (all.Length > 0) _last = all[0];
            }
            return For(_last);
        }

        public bool Apply(GameObject go) => ApplyCore(go, true);

        bool ApplyCore(GameObject go, bool select)
        {
            if (_window == null) return false;
            try
            {
                var view = _window.View;
                var vm = view?.ViewModel;
                if (vm == null || view.Source == null) return false;
                ClearHiddenSiblings(vm);
                CollapseAutoExpanded(view);
                if (go == null)
                {
                    vm.SetRoot(view.Source.Root);
                }
                else
                {
                    var handler = HierarchyExtensions.GetNodeTypeHandler<HierarchyGameObjectHandler>(view.Source);
                    if (handler == null) return false;
                    var node = handler.GetOrCreateNode(go);
                    var parent = view.Source.GetParent(node);
                    _filterParent = parent;
                    vm.SetRoot(parent);
                    HideSiblings(view, vm, node, parent);
                    RememberCollapsed(view, node);
                    view.Expand(node);
                    view.ExpandRecursive(node, HierarchyTraversalDirection.Children);
                }
                view.Update();
                if (select) UpdateSelection(go);
                return go != null;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Empathy Hierarchy] Filtering is not available here, jumping instead: " + e.Message);
                return false;
            }
        }

        /// <summary>Focusing a bookmark also selects it (Inspector shows it); ending the focus deselects it.</summary>
        void UpdateSelection(GameObject go)
        {
            if (go != null)
            {
                _selected = go;
                Selection.activeGameObject = go;
                return;
            }
            // only if it is still what the focus selected (the user may have picked something else meanwhile)
            if (_selected != null && Selection.activeGameObject == _selected) Deselect();
            _selected = null;
        }

        /// <summary>Clears both Unity's selection and the view's own row selection (which would otherwise come back).</summary>
        public void Deselect()
        {
            try { _window?.View?.DeselectAll(); }
            catch { /* view not ready */ }
            EHNav.ClearSelection();
        }

        /// <summary>The Hierarchy may rebuild its view after changes; keep the focus in place.</summary>
        public void KeepAlive(GameObject go)
        {
            if (_window == null) return;
            try
            {
                var view = _window.View;
                var handler = HierarchyExtensions.GetNodeTypeHandler<HierarchyGameObjectHandler>(view.Source);
                var node = handler.GetOrCreateNode(go);
                var parent = view.Source.GetParent(node);
                // re-root quietly: the user may have selected something else meanwhile
                if (view.ViewModel.GetRoot() != parent || parent != _filterParent) ApplyCore(go, false);
                else HideSiblings(view, view.ViewModel, node, parent); // new siblings may have appeared
            }
            catch { /* ignore */ }
        }

        void HideSiblings(HierarchyView view, HierarchyViewModel vm, HierarchyNode node, HierarchyNode parent)
        {
            int count = view.Source.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var sibling = view.Source.GetChild(parent, i);
                if (sibling == node) continue;
                try
                {
                    if (!vm.HasFlags(sibling, HierarchyNodeFlags.Hidden))
                    {
                        vm.SetFlags(sibling, HierarchyNodeFlags.Hidden);
                        _hiddenSiblings.Add(sibling);
                    }
                }
                catch { /* node not in the view model yet */ }
            }
        }

        /// <summary>Records every folded node in the subtree (only these get folded again later).</summary>
        void RememberCollapsed(HierarchyView view, HierarchyNode node)
        {
            var src = view.Source;
            _walk.Clear();
            _walk.Push(node);
            while (_walk.Count > 0)
            {
                var n = _walk.Pop();
                int count = src.GetChildrenCount(n);
                if (count == 0) continue;
                if (!view.IsExpanded(n)) _autoExpanded.Add(n);
                for (int i = 0; i < count; i++) _walk.Push(src.GetChild(n, i));
            }
        }

        void CollapseAutoExpanded(HierarchyView view)
        {
            if (_autoExpanded.Count == 0) return;
            foreach (var n in _autoExpanded)
            {
                try { if (view.Source.Exists(n)) view.Collapse(n); }
                catch { /* node removed meanwhile */ }
            }
            _autoExpanded.Clear();
        }

        void ClearHiddenSiblings(HierarchyViewModel vm)
        {
            foreach (var n in _hiddenSiblings)
            {
                try { vm.ClearFlags(n, HierarchyNodeFlags.Hidden); }
                catch { /* node was removed */ }
            }
            _hiddenSiblings.Clear();
        }
    }

    /// <summary>
    /// Rules + Memo Desk + Collapse All, placed at the right end of the Hierarchy's "Name" header row (left of its ⋮),
    /// so the navigation bar keeps its width for chips.
    /// </summary>
    class EHHeaderButtons : VisualElement
    {
        const string ColumnName = "HierarchyViewColumn Name";

        public static void Attach(VisualElement root, bool enabled)
        {
            var column = root.Q(ColumnName);
            var existing = root.Q<EHHeaderButtons>();
            var title = column?.Q<Label>("unity-multi-column-header-column-title");
            if (!enabled || column == null)
            {
                if (existing != null)
                {
                    existing.RemoveFromHierarchy();
                    if (title != null) title.style.marginRight = StyleKeyword.Null;
                }
                return;
            }
            if (existing != null) return;
            var host = column.Q(className: "unity-multi-column-header__column__content-container") ?? column;
            host.Add(new EHHeaderButtons());
            // the "Name" title shortens with "…" instead of running under the buttons
            if (title != null)
            {
                title.style.marginRight = 73;
                title.style.overflow = Overflow.Hidden;
                title.style.textOverflow = TextOverflow.Ellipsis;
            }
        }

        EHHeaderButtons()
        {
            style.position = Position.Absolute;
            style.right = 2; style.top = 0; style.bottom = 0;
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            Add(EHNavBar.HeaderButton(new EHRulesIcon { Tint = EHTheme.AccentText }, "Rules  (automatic looks by name or component)", () => EHRulesWindow.Open(null)));
            Add(EHNavBar.HeaderButton(new EHMemoIcon { Tint = EHTheme.AccentText }, "Memo Desk  (drag objects from the Hierarchy into it)", EHMemoDesk.Open));
            Add(EHNavBar.HeaderButton(new EHCollapseIcon { Tint = EHTheme.AccentText }, "Collapse all  (Alt+Shift+C)", () => EHMenus.CollapseAll()));
        }
    }
}
#endif
