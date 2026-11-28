using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace EmpathyHierarchy
{
    /// <summary>What a bookmark click does to a Hierarchy window.</summary>
    interface INavFilter
    {
        /// <summary>Focus on <paramref name="go"/> (null = undo). Returns true if the bookmark is now "on".</summary>
        bool Apply(GameObject go);
        /// <summary>Called after Hierarchy changes while a bookmark is on.</summary>
        void KeepAlive(GameObject go);
        /// <summary>Leaves nothing selected.</summary>
        void Deselect();
    }

    static class EHNavFilters
    {
        /// <summary>Filter for the Bookmarks window: the new Hierarchy when it is in use, otherwise the classic one.</summary>
        internal static INavFilter Current()
        {
#if UNITY_6000_5_OR_NEWER
            if (EHSettings.NewHierarchyActive) return EHNewHierarchyFilter.ForLast();
#endif
            return EHLegacyFilter.Instance;
        }
    }

    /// <summary>
    /// Classic Hierarchy can't hide rows, so a bookmark selects the object, unfolds it and scrolls to it;
    /// clicking it again puts the fold state back and deselects it.
    /// </summary>
    class EHLegacyFilter : INavFilter
    {
        internal static readonly EHLegacyFilter Instance = new EHLegacyFilter();

        object _snapshot;
        GameObject _target;
        bool _on;

        public bool Apply(GameObject go)
        {
            if (_on) Undo();
            if (go == null) return false;

            _snapshot = EHLegacyHierarchy.Snapshot();
            _target = go;
            _on = true;

            Selection.activeGameObject = go;
            EHLegacyHierarchy.SetExpanded(go, true, true);
            // ping after the unfold has been applied, so the scroll lands on the right row
            EditorApplication.delayCall += () => { if (go != null) EditorGUIUtility.PingObject(go); };
            return true;
        }

        void Undo()
        {
            _on = false;
            if (!EHLegacyHierarchy.Restore(_snapshot) && _target != null)
                EHLegacyHierarchy.SetExpanded(_target, false, true); // fallback: fold what we opened
            // deselect what the bookmark selected (unless the user picked something else meanwhile)
            if (_target != null && Selection.activeGameObject == _target) Deselect();
            _snapshot = null;
            _target = null;
        }

        public void KeepAlive(GameObject go) { }

        public void Deselect() => EHNav.ClearSelection();
    }

    /// <summary>Live bars, shared refresh and the Alt+1..9 shortcuts.</summary>
    [InitializeOnLoad]
    static class EHNav
    {
        internal static readonly List<EHNavBar> Live = new List<EHNavBar>();
        internal static string FlashId; // bookmark to highlight after the next rebuild (just added)

        static bool _refreshQueued, _labelsQueued;
        static readonly EditorApplication.CallbackFunction RefreshCallback = RefreshAll;
        static readonly EditorApplication.CallbackFunction LabelsCallback = RefreshLabelsAll;

        static EHNav()
        {
            EHBookmarks.Changed += QueueRefresh;
            EHStore.Changed += QueueRefresh;
            // hierarchy edits only need chip labels updated, not a full rebuild
            EditorApplication.hierarchyChanged += QueueLabels;
        }

        /// <summary>Remove mode: every chip shows a red × (toggled by the "−" chip). Shared by all bars.</summary>
        internal static bool RemoveMode;

        internal static void SetRemoveMode(bool on)
        {
            if (RemoveMode == on) return;
            RemoveMode = on;
            QueueRefresh();
        }

        internal static void QueueRefresh()
        {
            if (_refreshQueued) return;
            _refreshQueued = true;
            EditorApplication.delayCall += RefreshCallback;
        }

        static void RefreshAll()
        {
            _refreshQueued = false;
            if (RemoveMode && EHBookmarks.Items.Count == 0) RemoveMode = false; // nothing left to remove
            for (int i = Live.Count - 1; i >= 0; i--) Live[i].Rebuild();
        }

        static void QueueLabels()
        {
            if (_labelsQueued) return;
            _labelsQueued = true;
            EditorApplication.delayCall += LabelsCallback;
        }

        static void RefreshLabelsAll()
        {
            _labelsQueued = false;
            for (int i = Live.Count - 1; i >= 0; i--) Live[i].RefreshLabels();
        }

        /// <summary>
        /// Alt + Click on a Hierarchy row: add or remove the bookmark. The chip flashing (or disappearing) is the
        /// feedback; a message only appears when nothing could happen (the object has no saved ID).
        /// </summary>
        internal static void ToggleFromHierarchy(GameObject go, Action<string> notify)
        {
            if (go == null) return;
            bool had = EHBookmarks.IndexOf(go) >= 0;
            if (EHBookmarks.Toggle(go)) FlashId = EHBookmarks.IdOf(go);
            else if (!had)
            {
                var why = EHBookmarks.WhyNot(go);
                if (!NotifyCenter(why)) notify?.Invoke(why);
            }
        }

        static readonly UnityEngine.Object[] Nothing = new UnityEngine.Object[0];

        /// <summary>
        /// Deselects now and once more after the Hierarchy has re-rooted: the new Hierarchy re-applies its own
        /// row selection when the view changes, which would otherwise select the object again.
        /// </summary>
        internal static void ClearSelection()
        {
            Selection.objects = Nothing;
            EditorApplication.delayCall += () => Selection.objects = Nothing;
        }


        /// <summary>The focused Hierarchy (classic or new), else the first one open.</summary>
        internal static EditorWindow FindHierarchy()
        {
            if (IsHierarchy(EditorWindow.focusedWindow)) return EditorWindow.focusedWindow;
            foreach (var w in Resources.FindObjectsOfTypeAll<EditorWindow>())
                if (IsHierarchy(w)) return w;
            return null;
        }

        internal static bool IsHierarchy(EditorWindow w)
        {
            if (w == null) return false;
            var n = w.GetType().Name;
            return n == "SceneHierarchyWindow" || n == "HierarchyWindow";
        }

        static readonly GUIContent Message = new GUIContent();

        static System.Reflection.FieldInfo _parentField;

        /// <summary>False when the window is a background tab (e.g. Scene view behind the Game view). True if unsure.</summary>
        static bool IsTabShown(EditorWindow w)
        {
            try
            {
                if (_parentField == null) _parentField = typeof(EditorWindow).GetField("m_Parent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var host = _parentField?.GetValue(w);
                var actual = host?.GetType().GetProperty("actualView", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)?.GetValue(host);
                return actual == null || ReferenceEquals(actual, w);
            }
            catch { return true; }
        }

        /// <summary>Unity's usual centred message, in the Scene or Game view that is on screen.</summary>
        internal static bool NotifyCenter(string message)
        {
            // whichever of Scene / Game view is on screen (one of them usually is)
            EditorWindow scene = SceneView.lastActiveSceneView;
            EditorWindow target = scene != null && IsTabShown(scene) ? scene : null;
            if (target == null)
            {
                var gameType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                if (gameType != null)
                    foreach (var o in Resources.FindObjectsOfTypeAll(gameType))
                        if (o is EditorWindow g && IsTabShown(g)) { target = g; break; }
            }
            if (target == null) target = scene;
            if (target == null) return false;
            Message.text = message;
            target.ShowNotification(Message, 1.6);
            return true;
        }

        // ---- Alt+1..9: jump to bookmark N (can be turned off in settings or remapped in Edit > Shortcuts)

        [Shortcut("Empathy Hierarchy/Bookmark 1", KeyCode.Alpha1, ShortcutModifiers.Alt)] static void B1() => Activate(0);
        [Shortcut("Empathy Hierarchy/Bookmark 2", KeyCode.Alpha2, ShortcutModifiers.Alt)] static void B2() => Activate(1);
        [Shortcut("Empathy Hierarchy/Bookmark 3", KeyCode.Alpha3, ShortcutModifiers.Alt)] static void B3() => Activate(2);
        [Shortcut("Empathy Hierarchy/Bookmark 4", KeyCode.Alpha4, ShortcutModifiers.Alt)] static void B4() => Activate(3);
        [Shortcut("Empathy Hierarchy/Bookmark 5", KeyCode.Alpha5, ShortcutModifiers.Alt)] static void B5() => Activate(4);
        [Shortcut("Empathy Hierarchy/Bookmark 6", KeyCode.Alpha6, ShortcutModifiers.Alt)] static void B6() => Activate(5);
        [Shortcut("Empathy Hierarchy/Bookmark 7", KeyCode.Alpha7, ShortcutModifiers.Alt)] static void B7() => Activate(6);
        [Shortcut("Empathy Hierarchy/Bookmark 8", KeyCode.Alpha8, ShortcutModifiers.Alt)] static void B8() => Activate(7);
        [Shortcut("Empathy Hierarchy/Bookmark 9", KeyCode.Alpha9, ShortcutModifiers.Alt)] static void B9() => Activate(8);

        static void Activate(int index)
        {
            var s = EHSettings.instance;
            if (!s.enabled || !s.navBar || !s.navNumberShortcuts) return;
            ActiveBar()?.ActivateIndex(index);
        }

        /// <summary>The bar in the focused window, else under the mouse, else the Bookmarks window, else any.</summary>
        static EHNavBar ActiveBar()
        {
            foreach (var b in Live) if (b.Owner != null && b.Owner == EditorWindow.focusedWindow) return b;
            foreach (var b in Live) if (b.Owner != null && b.Owner == EditorWindow.mouseOverWindow) return b;
            foreach (var b in Live) if (b.IsTab) return b;
            return Live.Count > 0 ? Live[0] : null;
        }
    }

    /// <summary>
    /// Bookmark chips. Chip click = focus that object (new Hierarchy: show only it; classic: select, unfold, scroll),
    /// click again or "All" = back; double-click = select + ping; drag = reorder; Alt + Click = remove.
    /// Lives under the new Hierarchy's search field (Bar) or in the Bookmarks window (Tab), horizontal or vertical.
    /// </summary>
    class EHNavBar : VisualElement
    {
        const string FoldPrefKey = "EmpathyHierarchy.NavBarFolded";
        /// <summary>Chip height from Settings (20–28, default 22); text and icons follow it.</summary>
        internal static float ChipH => Mathf.Clamp(Mathf.Round(EHSettings.instance.bookmarkChipSize), 20f, 28f);
        internal static int ChipFont => Mathf.Clamp(Mathf.RoundToInt(ChipH * 0.55f), 11, 14);

        internal readonly EditorWindow Owner;
        internal readonly bool IsTab;
        readonly Func<INavFilter> _filterSource;
        readonly VisualElement _chips;
        readonly VisualElement _buttons;
        readonly Button _foldButton;
        readonly VisualElement _ghost;
        readonly Label _ghostLabel;
        readonly List<EHNavChip> _chipList = new List<EHNavChip>();

        BookmarkEntry _active;      // focused bookmark (null = All)
        INavFilter _activeFilter;
        int _ghostIndex = -1;
        internal bool Reordering;   // chips slide into place only while a chip is being dragged
        internal EHNavChip DraggedChip; // hidden while dragged; the ghost takes its place
        internal bool Vertical { get; private set; }
        /// <summary>Area that accepts drops; dragging a chip outside it hands the object to Unity's drag (Memo Desk, fields…).</summary>
        internal VisualElement DragArea { get; private set; }

        internal const string DragKey = "EmpathyHierarchy.Bookmark";

        static readonly GUIContent Notice = new GUIContent();

        public EHNavBar(EditorWindow owner, Func<INavFilter> filterSource, bool tab)
        {
            Owner = owner;
            IsTab = tab;
            _filterSource = filterSource;
            name = "empathy-navigation-bar";
            style.backgroundColor = EHEditorColors.BarBg;
            if (!tab)
            {
                // Unity-native look: the bar should read as part of the Hierarchy toolbar
                style.borderBottomWidth = 1;
                style.borderBottomColor = EHEditorColors.Divider;
            }

            _chips = new VisualElement();
            _chips.style.flexGrow = 1;
            _chips.style.flexShrink = 1;
            _chips.style.overflow = Overflow.Hidden;

            _buttons = new VisualElement();
            _buttons.style.flexDirection = FlexDirection.Row;
            _buttons.style.flexShrink = 0;
            if (tab)
            {
                // Rules / Memo / Collapse live on the Hierarchy itself (scene row or Name header), not here
                _buttons.style.display = DisplayStyle.None;
            }
            else
            {
                // memo + collapse-all live in the "Name" header row; the fold stays here, easy to spot
                _foldButton = IconButton("▲", "Show one row only", ToggleFold);
                _foldButton.style.backgroundColor = EHTheme.Accent;
                _foldButton.style.color = EHTheme.AccentText;
                _foldButton.style.unityFontStyleAndWeight = FontStyle.Bold;
                _foldButton.RegisterCallback<PointerEnterEvent>(_ => _foldButton.style.backgroundColor = EHDraw.Lighten(EHTheme.Accent, 0.12f));
                _foldButton.RegisterCallback<PointerLeaveEvent>(_ => _foldButton.style.backgroundColor = EHTheme.Accent);
                _buttons.Add(_foldButton);
            }

            // placeholder that shows where a chip being reordered will land
            _ghost = new VisualElement { pickingMode = PickingMode.Ignore };
            StyleChip(_ghost);
            _ghost.style.backgroundColor = EHDraw.WithAlpha(EHTheme.Point, 0.15f);
            SetBorder(_ghost, EHTheme.Point, 1f);
            _ghostLabel = new Label { pickingMode = PickingMode.Ignore };
            StyleChipLabel(_ghostLabel);
            _ghostLabel.style.color = EHTheme.Point;
            _ghost.Add(_ghostLabel);

            RegisterCallback<AttachToPanelEvent>(_ => { if (!EHNav.Live.Contains(this)) EHNav.Live.Add(this); });
            RegisterCallback<DetachFromPanelEvent>(_ => EHNav.Live.Remove(this));

            if (!tab) RegisterDropTarget(this);
            ApplyLayout(false, true);
            ApplyFold();
            Rebuild();
        }

        // ------------------------------------------------------------------ drag & drop in

        /// <summary>Objects dragged from the Hierarchy (or a chip dragged back in) can be dropped onto <paramref name="area"/>.</summary>
        internal void RegisterDropTarget(VisualElement area)
        {
            DragArea = area;
            area.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            area.RegisterCallback<DragPerformEvent>(OnDragPerform);
            area.RegisterCallback<DragLeaveEvent>(_ => HideGhost());
            area.RegisterCallback<DragExitedEvent>(_ => HideGhost());
        }

        static readonly List<GameObject> DropList = new List<GameObject>();

        static List<GameObject> DropObjects()
        {
            DropList.Clear();
            var refs = DragAndDrop.objectReferences;
            if (refs == null) return DropList;
            foreach (var o in refs)
                if (o is GameObject go && !EditorUtility.IsPersistent(go)) DropList.Add(go);
            return DropList;
        }

        void OnDragUpdated(DragUpdatedEvent e)
        {
            var objs = DropObjects();
            if (objs.Count == 0) return;
            bool own = DragAndDrop.GetGenericData(DragKey) is BookmarkEntry;
            DragAndDrop.visualMode = own ? DragAndDropVisualMode.Move : DragAndDropVisualMode.Copy;
            ShowGhost(InsertIndex(e.mousePosition), objs.Count == 1 ? EHCache.Name(objs[0]) : objs.Count + " objects");
            e.StopPropagation();
        }

        void OnDragPerform(DragPerformEvent e)
        {
            var objs = DropObjects();
            if (objs.Count == 0) return;
            DragAndDrop.AcceptDrag();
            int index = ListIndex(InsertIndex(e.mousePosition));
            HideGhost();
            if (DragAndDrop.GetGenericData(DragKey) is BookmarkEntry entry && EHBookmarks.Items.Contains(entry))
            {
                EHBookmarks.Move(entry, index);
            }
            else
            {
                var dropped = new List<GameObject>(objs);
                EHBookmarks.Add(dropped, index);
                EHNav.FlashId = EHBookmarks.IdOf(dropped[0]); // the bars rebuild on the next tick and flash it
                foreach (var go in dropped)
                    if (EHBookmarks.IndexOf(go) < 0) { Notify(EHBookmarks.WhyNot(go)); break; }
            }
            e.StopPropagation();
        }


        // ------------------------------------------------------------------ layout

        /// <summary>Horizontal: chips wrap in rows, buttons on the right. Vertical: buttons on top, one chip per row.</summary>
        internal void SetVertical(bool vertical) => ApplyLayout(vertical, false);

        void ApplyLayout(bool vertical, bool force)
        {
            if (!force && vertical == Vertical) return;
            Vertical = vertical;
            Clear();
            if (vertical)
            {
                style.flexDirection = FlexDirection.Column;
                style.alignItems = Align.Stretch;
                style.paddingLeft = 6; style.paddingRight = 6; style.paddingTop = 6; style.paddingBottom = 2;
                _buttons.style.justifyContent = Justify.FlexEnd;
                _buttons.style.marginBottom = 6;
                _chips.style.flexDirection = FlexDirection.Column;
                _chips.style.flexWrap = Wrap.NoWrap;
                Add(_buttons);
                Add(_chips);
            }
            else
            {
                style.flexDirection = FlexDirection.Row;
                style.alignItems = Align.FlexStart;
                style.paddingLeft = 6; style.paddingRight = 4; style.paddingTop = 5; style.paddingBottom = 0;
                _buttons.style.justifyContent = Justify.FlexStart;
                _buttons.style.marginBottom = 0;
                _chips.style.flexDirection = FlexDirection.Row;
                _chips.style.flexWrap = Wrap.Wrap;
                Add(_chips);
                Add(_buttons);
            }
            if (!force) Rebuild();
        }

        // ------------------------------------------------------------------ build

        public void Rebuild()
        {
            HideGhost();
            _chips.Clear();
            _chipList.Clear();

            _chips.Add(MakeTextChip("All", _active == null, () =>
            {
                SetFilter(null);
                // "All" always ends with nothing selected (in the view too, or the new Hierarchy re-selects it later)
                var f = _filterSource?.Invoke();
                if (f != null) f.Deselect();
                else EHNav.ClearSelection();
            }));

            string lastScene = null;
            var items = EHBookmarks.Items;
            for (int i = 0; i < items.Count; i++)
            {
                var entry = items[i];
                var go = EHBookmarks.Resolve(entry);
                if (go == null) continue; // scene not loaded

                if (lastScene != null && lastScene != entry.scene) _chips.Add(Divider());
                lastScene = entry.scene;

                var chip = new EHNavChip(this, entry, go, entry == _active);
                _chipList.Add(chip);
                _chips.Add(chip);
                if (EHNav.FlashId != null && entry.id == EHNav.FlashId) chip.Flash();
            }

            // "+" only explains how to add (adding is Alt+click, so nothing gets added by accident)
            var tools = new VisualElement();
            tools.style.flexDirection = FlexDirection.Row;
            tools.style.flexShrink = 0;
            tools.style.alignSelf = Align.FlexStart;
            tools.Add(MakePlusChip());
            if (_chipList.Count > 0 || EHNav.RemoveMode) tools.Add(MakeMinusChip());
            _chips.Add(tools);

            ReapplyFilterIfLost();
            // every live bar gets a chance to flash before the id is cleared
            EditorApplication.delayCall += () => EHNav.FlashId = null;
        }

        /// <summary>Cheap update after hierarchy edits: rename chips, rebuild only if a bookmark vanished.</summary>
        public void RefreshLabels()
        {
            foreach (var chip in _chipList)
            {
                if (chip.Target == null) { Rebuild(); return; }
                chip.SetName(EHCache.Name(chip.Target));
            }
            ReapplyFilterIfLost();
        }

        internal void StyleForLayout(VisualElement chip)
        {
            if (!Vertical) return;
            chip.style.marginRight = 0;
            chip.style.alignSelf = Align.Stretch;
        }

        /// <summary>Round "+": says how to add (drag in, or Alt + Click in the Hierarchy).</summary>
        VisualElement MakePlusChip()
        {
            var hint = IsTab ? "Drag objects into the Bookmarks window, or Alt + Click them in the Hierarchy"
                             : "Alt + Click an object in the Hierarchy to add it";
            var chip = MakeRoundChip("+", false, () => Notify(hint));
            chip.tooltip = hint + "\n(Alt + Click it again, or Alt + Click its bookmark, to remove)";
            return chip;
        }

        /// <summary>Round "−": remove mode on/off (each chip then shows a red ×).</summary>
        VisualElement MakeMinusChip()
        {
            var chip = MakeRoundChip("−", EHNav.RemoveMode, () => EHNav.SetRemoveMode(!EHNav.RemoveMode));
            chip.tooltip = EHNav.RemoveMode ? "Done removing" : "Remove bookmarks (shows × on each)";
            return chip;
        }

        /// <summary>Round chip with a beige ring; when <paramref name="on"/> it is filled red (like Memo Desk's delete mode).</summary>
        VisualElement MakeRoundChip(string glyph, bool on, Action click)
        {
            var chip = new VisualElement();
            chip.style.width = ChipH; chip.style.height = ChipH;
            chip.style.marginRight = 5; chip.style.marginBottom = 5;
            chip.style.alignItems = Align.Center;
            chip.style.justifyContent = Justify.Center;
            chip.style.flexShrink = 0;
            SetRadius(chip, ChipH * 0.5f);
            var label = new Label(glyph) { pickingMode = PickingMode.Ignore };
            label.style.fontSize = Mathf.RoundToInt(ChipH * 0.75f);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.paddingLeft = 0; label.style.paddingRight = 0; label.style.paddingTop = 0; label.style.paddingBottom = 2;
            label.style.marginLeft = 0; label.style.marginRight = 0; label.style.marginTop = 0; label.style.marginBottom = 0;
            chip.Add(label);

            Color ring = EditorGUIUtility.isProSkin ? EHTheme.Accent : new Color(0.498f, 0.333f, 0.208f);
            Action<bool> paint = hover =>
            {
                if (on)
                {
                    chip.style.backgroundColor = hover ? EHDraw.Lighten(MemoUI.Danger, 0.12f) : MemoUI.Danger;
                    SetBorder(chip, MemoUI.Danger, 1.5f);
                    label.style.color = Color.white;
                    return;
                }
                chip.style.backgroundColor = hover ? EHTheme.Accent : EHEditorColors.Button;
                SetBorder(chip, ring, 1.5f);
                label.style.color = hover ? EHTheme.AccentText : ring;
            };
            paint(false);
            chip.RegisterCallback<PointerEnterEvent>(_ => paint(true));
            chip.RegisterCallback<PointerLeaveEvent>(_ => paint(false));
            chip.RegisterCallback<PointerUpEvent>(e => { if (e.button == 0) click(); });
            return chip;
        }

        VisualElement MakeTextChip(string text, bool on, Action click)
        {
            var chip = new VisualElement();
            StyleChip(chip);
            StyleForLayout(chip);
            var label = new Label(text);
            StyleChipLabel(label);
            chip.Add(label);
            Paint(chip, label, on, false);
            chip.RegisterCallback<PointerEnterEvent>(e => Paint(chip, label, on, true));
            chip.RegisterCallback<PointerLeaveEvent>(e => Paint(chip, label, on, false));
            chip.RegisterCallback<PointerUpEvent>(e => { if (e.button == 0) click(); });
            return chip;
        }

        VisualElement Divider()
        {
            var d = new VisualElement { pickingMode = PickingMode.Ignore };
            d.style.backgroundColor = EHEditorColors.Divider;
            if (Vertical)
            {
                d.style.height = 1;
                d.style.marginTop = 1; d.style.marginBottom = 6; d.style.marginLeft = 4; d.style.marginRight = 4;
            }
            else
            {
                d.style.width = 1; d.style.height = ChipH - 6;
                d.style.marginLeft = 2; d.style.marginRight = 7; d.style.marginTop = 3;
            }
            return d;
        }

        Button IconButton(string text, string tooltip, Action click)
        {
            var b = new Button(click) { text = text, tooltip = tooltip };
            b.style.width = 26; b.style.height = ChipH;
            b.style.marginLeft = 3; b.style.marginRight = 0; b.style.marginTop = 0;
            b.style.paddingLeft = 0; b.style.paddingRight = 0;
            b.style.unityTextAlign = TextAnchor.MiddleCenter;
            b.style.fontSize = 13;
            b.style.backgroundColor = EHEditorColors.Button;
            b.style.color = EHEditorColors.Text;
            b.RegisterCallback<PointerEnterEvent>(_ => b.style.backgroundColor = EHEditorColors.ButtonHover);
            b.RegisterCallback<PointerLeaveEvent>(_ => b.style.backgroundColor = EHEditorColors.Button);
            SetBorder(b, Color.clear, 0f);
            SetRadius(b, ChipH * 0.5f);
            return b;
        }

        /// <summary>Small beige icon button (Name header of the new Hierarchy, Bookmarks window).</summary>
        internal static VisualElement HeaderButton(VisualElement icon, string tooltip, Action click)
        {
            var b = new VisualElement { tooltip = tooltip };
            b.style.width = 20; b.style.height = 16;
            b.style.marginLeft = 3;
            b.style.backgroundColor = EHTheme.Accent;
            SetRadius(b, 4);
            b.Add(icon);
            b.RegisterCallback<PointerEnterEvent>(_ => b.style.backgroundColor = EHDraw.Lighten(EHTheme.Accent, 0.12f));
            b.RegisterCallback<PointerLeaveEvent>(_ => b.style.backgroundColor = EHTheme.Accent);
            // a header sorts / drags columns on click: keep those clicks for the button only
            b.RegisterCallback<PointerDownEvent>(e =>
            {
                e.StopImmediatePropagation();
                if (e.button == 0) click();
            });
            b.RegisterCallback<PointerUpEvent>(e => e.StopImmediatePropagation());
            b.RegisterCallback<ClickEvent>(e => e.StopImmediatePropagation());
            return b;
        }

        internal void Notify(string message)
        {
            // bookmark windows / bars are too small for a readable message: use the middle of the editor
            if (EHNav.NotifyCenter(message)) return;
            if (Owner == null) return;
            Notice.text = message;
            Owner.ShowNotification(Notice, 1.6);
        }

        // ------------------------------------------------------------------ fold (Bar only)

        static bool Folded => EditorPrefs.GetBool(FoldPrefKey, false);

        void ToggleFold()
        {
            EditorPrefs.SetBool(FoldPrefKey, !Folded);
            ApplyFold();
        }

        void ApplyFold()
        {
            if (_foldButton == null) return;
            bool folded = Folded;
            if (folded) _chips.style.maxHeight = ChipH + 5f;
            else _chips.style.maxHeight = StyleKeyword.None;
            _foldButton.text = folded ? "▼" : "▲";
            _foldButton.tooltip = folded ? "Show all rows" : "Show one row only";
        }

        // ------------------------------------------------------------------ focus

        internal void OnChipClicked(EHNavChip chip) => SetFilter(chip.Entry == _active ? null : chip.Entry);

        internal void ActivateIndex(int index)
        {
            if (index < 0 || index >= _chipList.Count) return;
            OnChipClicked(_chipList[index]);
        }

        internal static void Jump(GameObject go)
        {
            if (go == null) return;
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
        }

        void SetFilter(BookmarkEntry entry)
        {
            var go = entry != null ? EHBookmarks.Resolve(entry) : null;
            if (entry != null && go == null) return;

            var filter = _filterSource?.Invoke();
            if (_activeFilter != null && _activeFilter != filter) _activeFilter.Apply(null); // target window changed
            bool on = filter != null && filter.Apply(go);
            _active = on ? entry : null;
            _activeFilter = on ? filter : null;
            if (entry != null && !on) Jump(go); // focusing not available: just jump there
            Rebuild();
        }

        /// <summary>Keeps the focused bookmark in place after Hierarchy changes; drops it if it was removed.</summary>
        void ReapplyFilterIfLost()
        {
            if (_active == null) return;
            var go = EHBookmarks.Resolve(_active);
            if (go == null || EHBookmarks.Items.IndexOf(_active) < 0)
            {
                _active = null;
                _activeFilter?.Apply(null);
                _activeFilter = null;
                return;
            }
            _activeFilter?.KeepAlive(go);
        }

        // ------------------------------------------------------------------ chip reordering

        /// <summary>Position among the visible chips (0..count) for a point in world space.</summary>
        internal int InsertIndex(Vector2 world)
        {
            for (int i = 0; i < _chipList.Count; i++)
            {
                if (_chipList[i] == DraggedChip) continue; // only the other chips count
                var r = _chipList[i].worldBound;
                if (Vertical)
                {
                    if (world.y < r.center.y) return i;
                    continue;
                }
                if (world.y < r.yMin) return i;                      // earlier row
                if (world.y <= r.yMax && world.x < r.center.x) return i;
            }
            return _chipList.Count;
        }

        /// <summary>Converts a visible-chip index into an index in the full bookmark list.</summary>
        int ListIndex(int visibleIndex)
        {
            var items = EHBookmarks.Items;
            if (visibleIndex < _chipList.Count) return items.IndexOf(_chipList[visibleIndex].Entry);
            return items.Count;
        }

        internal void ShowGhost(int visibleIndex, string label, float width = -1f)
        {
            _ghostLabel.text = label;
            // same size as the dragged chip, so the drop lands without a jump
            if (width > 0f && !Vertical) _ghost.style.width = width;
            else _ghost.style.width = StyleKeyword.Auto;
            if (_ghostIndex == visibleIndex && _ghost.parent == _chips) return;
            _ghostIndex = visibleIndex;
            _ghost.RemoveFromHierarchy();
            StyleForLayout(_ghost);
            // chips: [All] [divider?] [chip...] [+]; insert before the chip at visibleIndex
            int at = visibleIndex < _chipList.Count ? _chips.IndexOf(_chipList[visibleIndex]) : _chips.childCount - 1;
            _chips.Insert(Mathf.Max(1, at), _ghost);
        }

        internal void HideGhost()
        {
            _ghostIndex = -1;
            _ghost.RemoveFromHierarchy();
        }

        internal void EndChipDrag(EHNavChip chip, Vector2 world)
        {
            // keep the ghost (and the chip hidden) until the rebuild draws the chip in that spot: no flash of the old order
            int visible = InsertIndex(world);
            EHBookmarks.Move(chip.Entry, ListIndex(visible)); // saves → every bar rebuilds on the next tick
        }

        // ------------------------------------------------------------------ styling helpers

        internal static void StyleChip(VisualElement e)
        {
            e.style.flexDirection = FlexDirection.Row;
            e.style.alignItems = Align.Center;
            e.style.height = ChipH;
            e.style.flexShrink = 0;
            e.style.paddingLeft = 10; e.style.paddingRight = 10;
            e.style.marginRight = 5; e.style.marginBottom = 5;
            SetRadius(e, ChipH * 0.5f);
        }

        internal static void StyleChipLabel(Label l)
        {
            l.style.fontSize = ChipFont;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.unityTextAlign = TextAnchor.MiddleLeft;
            l.style.paddingLeft = 0; l.style.paddingRight = 0; l.style.paddingTop = 0; l.style.paddingBottom = 0;
            l.style.marginLeft = 0; l.style.marginRight = 0;
            l.style.whiteSpace = WhiteSpace.NoWrap;
            l.style.maxWidth = 140;
            l.style.flexShrink = 1;
            l.style.overflow = Overflow.Hidden;
            l.style.textOverflow = TextOverflow.Ellipsis;
        }

        /// <summary>"All" chip.</summary>
        internal static void Paint(VisualElement chip, Label label, bool on, bool hover)
        {
            chip.style.backgroundColor = on ? EHTheme.Accent : hover ? EHEditorColors.ButtonHover : EHEditorColors.Button;
            label.style.color = on ? EHTheme.AccentText : EHEditorColors.Text;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
        }

        /// <summary>
        /// Bookmark chip: background = the object's color (text black/white for contrast),
        /// or the dark default when it has no color. The active chip gets a beige ring.
        /// </summary>
        internal static void PaintBookmark(VisualElement chip, Label label, Color? fill, bool on, bool hover)
        {
            Color bg = fill ?? EHEditorColors.Button;
            if (hover) bg = fill.HasValue ? EHDraw.Lighten(bg, 0.12f) : EHEditorColors.ButtonHover;
            chip.style.backgroundColor = bg;
            label.style.color = fill.HasValue ? EHDraw.ContrastText(bg) : EHEditorColors.Text;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            SetBorder(chip, on ? EHTheme.Accent : Color.clear, on ? 2f : 0f);
        }

        internal static void SetRadius(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = r; e.style.borderTopRightRadius = r;
            e.style.borderBottomLeftRadius = r; e.style.borderBottomRightRadius = r;
        }

        internal static void SetBorder(VisualElement e, Color c, float w)
        {
            e.style.borderTopWidth = w; e.style.borderBottomWidth = w;
            e.style.borderLeftWidth = w; e.style.borderRightWidth = w;
            e.style.borderTopColor = c; e.style.borderBottomColor = c;
            e.style.borderLeftColor = c; e.style.borderRightColor = c;
        }
    }

    /// <summary>Note page with three lines (Memo Desk button).</summary>
    class EHMemoIcon : VisualElement
    {
        public Color? Tint;

        public EHMemoIcon()
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0; style.right = 0; style.top = 0; style.bottom = 0;
            generateVisualContent += Draw;
        }

        static void Draw(MeshGenerationContext ctx)
        {
            var c = ctx.visualElement.contentRect.center;
            var p = ctx.painter2D;
            p.strokeColor = (ctx.visualElement as EHMemoIcon)?.Tint ?? EHEditorColors.Text;
            p.lineWidth = 1.4f;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;

            // page with a folded corner
            p.BeginPath();
            p.MoveTo(new Vector2(c.x - 4.5f, c.y - 6f));
            p.LineTo(new Vector2(c.x + 2f, c.y - 6f));
            p.LineTo(new Vector2(c.x + 4.5f, c.y - 3.5f));
            p.LineTo(new Vector2(c.x + 4.5f, c.y + 6f));
            p.LineTo(new Vector2(c.x - 4.5f, c.y + 6f));
            p.ClosePath();
            p.Stroke();

            for (int i = 0; i < 3; i++)
            {
                float y = c.y - 2f + i * 2.8f;
                p.BeginPath();
                p.MoveTo(new Vector2(c.x - 2.5f, y));
                p.LineTo(new Vector2(c.x + (i == 2 ? 0.5f : 2.5f), y));
                p.Stroke();
            }
        }
    }

    /// <summary>Rules glyph: three lines, each with a dot in front (a list of "if → look").</summary>
    class EHRulesIcon : VisualElement
    {
        public Color? Tint;

        public EHRulesIcon()
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0; style.right = 0; style.top = 0; style.bottom = 0;
            generateVisualContent += Draw;
        }

        static void Draw(MeshGenerationContext ctx)
        {
            var c = ctx.visualElement.contentRect.center;
            var p = ctx.painter2D;
            var tint = (ctx.visualElement as EHRulesIcon)?.Tint ?? EHEditorColors.Text;
            p.strokeColor = tint;
            p.fillColor = tint;
            p.lineWidth = 1.4f;
            p.lineCap = LineCap.Round;
            for (int i = 0; i < 3; i++)
            {
                float y = c.y - 4f + i * 4f;
                p.BeginPath();
                p.Arc(new Vector2(c.x - 4.5f, y), 1.2f, 0f, 360f);
                p.Fill();
                p.BeginPath();
                p.MoveTo(new Vector2(c.x - 1.5f, y));
                p.LineTo(new Vector2(c.x + 5f, y));
                p.Stroke();
            }
        }
    }

    /// <summary>Collapse-all glyph: a line at the bottom with an arrow rising from it.</summary>
    class EHCollapseIcon : VisualElement
    {
        public Color? Tint;

        public EHCollapseIcon()
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0; style.right = 0; style.top = 0; style.bottom = 0;
            generateVisualContent += Draw;
        }

        static void Draw(MeshGenerationContext ctx)
        {
            var r = ctx.visualElement.contentRect;
            var c = r.center;
            var p = ctx.painter2D;
            var tint = (ctx.visualElement as EHCollapseIcon)?.Tint ?? EHEditorColors.Text;
            p.strokeColor = tint;
            p.fillColor = tint;
            p.lineWidth = 1.6f;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;

            // baseline
            p.BeginPath();
            p.MoveTo(new Vector2(c.x - 5f, c.y + 5f));
            p.LineTo(new Vector2(c.x + 5f, c.y + 5f));
            p.Stroke();

            // shaft
            p.BeginPath();
            p.MoveTo(new Vector2(c.x, c.y + 2.5f));
            p.LineTo(new Vector2(c.x, c.y - 3f));
            p.Stroke();

            // arrow head
            p.BeginPath();
            p.MoveTo(new Vector2(c.x - 4f, c.y - 1.5f));
            p.LineTo(new Vector2(c.x, c.y - 6f));
            p.LineTo(new Vector2(c.x + 4f, c.y - 1.5f));
            p.ClosePath();
            p.Fill();
        }
    }

    /// <summary>One bookmark chip: icon, name; drag to reorder; Alt + Click to remove.</summary>
    class EHNavChip : VisualElement
    {
        public readonly BookmarkEntry Entry;
        readonly EHNavBar _bar;
        readonly GameObject _go;
        readonly Label _label;
        readonly Color? _fill;
        readonly bool _on;

        Vector2 _downPos;
        bool _pressed, _dragging;
        float _dragWidth;
        int _pointerId = -1;

        public EHNavChip(EHNavBar bar, BookmarkEntry entry, GameObject go, bool on)
        {
            _bar = bar;
            Entry = entry;
            _go = go;
            _on = on;
            EHNavBar.StyleChip(this);
            bar.StyleForLayout(this);
            style.paddingLeft = 7;

            var s = EHSettings.instance;
            var st = EHStore.Get(go);
            if (st != null && st.hasColor) _fill = EHDraw.Opaque(st.Color1);

            // object icon (custom icon from Edit Style if set)
            if (EHIcons.Resolve(go, st, s.componentAwareIcons) is Texture2D tex)
            {
                var icon = new Image { image = tex, pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.ScaleToFit };
                icon.style.width = EHNavBar.ChipH - 8f; icon.style.height = EHNavBar.ChipH - 8f; icon.style.marginRight = 5;
                icon.style.flexShrink = 0;
                if (st != null && st.iconDarken > 0f) icon.tintColor = st.IconTint;
                Add(icon);
            }

            _label = new Label(EHCache.Name(go)) { pickingMode = PickingMode.Ignore };
            EHNavBar.StyleChipLabel(_label);
            if (bar.Vertical) _label.style.maxWidth = StyleKeyword.None;
            Add(_label);

            if (EHNav.RemoveMode) Add(MakeRemoveButton());

            EHNavBar.PaintBookmark(this, _label, _fill, _on, false);
            RegisterCallback<PointerEnterEvent>(e => EHNavBar.PaintBookmark(this, _label, _fill, _on, true));
            RegisterCallback<PointerLeaveEvent>(e => EHNavBar.PaintBookmark(this, _label, _fill, _on, false));
            EHSlide.Attach(this, () => _bar.Reordering);
            RegisterCallback<PointerDownEvent>(OnDown);
            RegisterCallback<PointerMoveEvent>(OnMove);
            RegisterCallback<PointerUpEvent>(OnUp);
        }

        public GameObject Target => _go;

        /// <summary>Small red × at the end of the chip (remove mode).</summary>
        VisualElement MakeRemoveButton()
        {
            var x = new VisualElement();
            x.style.width = 16; x.style.height = 16;
            x.style.marginLeft = 6; x.style.marginRight = -3;
            x.style.flexShrink = 0;
            x.style.alignItems = Align.Center;
            x.style.justifyContent = Justify.Center;
            x.style.backgroundColor = MemoUI.Danger;
            EHNavBar.SetRadius(x, 8);
            if (_bar.Vertical) x.style.marginLeft = StyleKeyword.Auto; // right edge of the row
            else
            {
                // over the chip's right end, so the chip keeps its size
                x.style.position = Position.Absolute;
                x.style.right = 4;
                x.style.top = (EHNavBar.ChipH - 16f) * 0.5f - 0.5f;
                x.style.marginLeft = 0; x.style.marginRight = 0;
            }
            var glyph = new Label("×") { pickingMode = PickingMode.Ignore };
            glyph.style.color = Color.white;
            glyph.style.fontSize = 12;
            glyph.style.unityFontStyleAndWeight = FontStyle.Bold;
            glyph.style.unityTextAlign = TextAnchor.MiddleCenter;
            glyph.style.paddingLeft = 0; glyph.style.paddingRight = 0; glyph.style.paddingTop = 0; glyph.style.paddingBottom = 1;
            glyph.style.marginLeft = 0; glyph.style.marginRight = 0; glyph.style.marginTop = 0; glyph.style.marginBottom = 0;
            x.Add(glyph);
            x.RegisterCallback<PointerEnterEvent>(_ => x.style.backgroundColor = EHDraw.Lighten(MemoUI.Danger, 0.15f));
            x.RegisterCallback<PointerLeaveEvent>(_ => x.style.backgroundColor = MemoUI.Danger);
            x.RegisterCallback<PointerDownEvent>(e =>
            {
                e.StopImmediatePropagation(); // not a click / drag on the chip
                if (e.button == 0) EHBookmarks.Remove(Entry);
            });
            x.RegisterCallback<PointerUpEvent>(e => e.StopImmediatePropagation());
            return x;
        }

        public void SetName(string n)
        {
            if (!ReferenceEquals(_label.text, n)) _label.text = n;
        }

        /// <summary>
        /// Three orange pulses on the background so you can see what was just added.
        /// Only the color changes (no border), so the chip never changes size.
        /// </summary>
        public void Flash()
        {
            const int frames = 36;          // 36 x 40ms = 1.4s
            Color baseColor = _fill ?? EHEditorColors.Button;
            Color glow = Color.Lerp(baseColor, EHTheme.Point, 0.85f);
            int frame = 0;
            schedule.Execute(() =>
            {
                frame++;
                float t = frame / (float)frames;
                float k = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 3f)); // three clear pulses
                var bg = Color.Lerp(baseColor, glow, Mathf.Sqrt(k));
                style.backgroundColor = bg;
                _label.style.color = EHDraw.ContrastText(bg);
                if (frame >= frames) EHNavBar.PaintBookmark(this, _label, _fill, _on, false);
            }).Every(40).Until(() => frame >= frames);
        }

        void OnDown(PointerDownEvent e)
        {
            if (e.button != 0) return;
            if (e.altKey)
            {
                // removing works like adding: Alt + Click, so a stray click never deletes a bookmark
                e.StopPropagation();
                EHBookmarks.Remove(Entry);
                return;
            }
            if (e.clickCount == 2)
            {
                EHNavBar.Jump(_go);
                e.StopPropagation();
                return;
            }
            _pressed = true;
            _dragging = false;
            _downPos = e.position;
            _pointerId = e.pointerId;
            this.CapturePointer(_pointerId);
            e.StopPropagation();
        }

        void OnMove(PointerMoveEvent e)
        {
            if (!_pressed || !this.HasPointerCapture(_pointerId)) return;
            if (!_dragging && (((Vector2)e.position) - _downPos).sqrMagnitude > 16f)
            {
                _dragging = true;
                _bar.Reordering = true;
                _bar.DraggedChip = this;
                _dragWidth = layout.width;
                // the chip leaves its spot (the ghost shows where it goes), so it never drops onto itself
                _bar.ShowGhost(_bar.InsertIndex(e.position), EHCache.Name(_go), _dragWidth);
                style.display = DisplayStyle.None;
            }
            if (!_dragging) return;
            var area = _bar.DragArea ?? _bar;
            if (!area.worldBound.Contains(e.position))
            {
                StartExternalDrag();
                return;
            }
            _bar.ShowGhost(_bar.InsertIndex(e.position), EHCache.Name(_go), _dragWidth);
        }

        /// <summary>Left the bookmarks: continue as a normal Unity drag of the object (Memo Desk, object fields, Hierarchy…).</summary>
        void StartExternalDrag()
        {
            if (this.HasPointerCapture(_pointerId)) this.ReleasePointer(_pointerId);
            _pressed = false;
            _dragging = false;
            _bar.Reordering = false;
            _bar.DraggedChip = null;
            _bar.HideGhost();
            style.display = DisplayStyle.Flex;
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.objectReferences = new UnityEngine.Object[] { _go };
            DragAndDrop.SetGenericData(EHNavBar.DragKey, Entry);
            DragAndDrop.StartDrag(EHCache.Name(_go));
        }

        void OnUp(PointerUpEvent e)
        {
            if (!_pressed) return;
            _pressed = false;
            if (this.HasPointerCapture(_pointerId)) this.ReleasePointer(_pointerId);
            if (_dragging)
            {
                _dragging = false;
                _bar.Reordering = false;
                _bar.EndChipDrag(this, e.position); // stays hidden; the rebuild replaces it
                _bar.DraggedChip = null;
            }
            else if (e.button == 0)
            {
                _bar.OnChipClicked(this);
            }
            e.StopPropagation();
        }
    }
}
