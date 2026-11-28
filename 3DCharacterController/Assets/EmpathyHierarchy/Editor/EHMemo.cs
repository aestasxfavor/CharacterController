#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace EmpathyHierarchy
{
    // ====================================================================== data

    [Serializable]
    public class MemoEntry
    {
        public string id;            // GlobalObjectId of the GameObject
        public string name;          // last known name / look, shown when the scene isn't open
        public bool hasColor;
        public Color color = Color.gray, color2 = Color.gray;
        public FillMode fill = FillMode.Solid;
        public GradientMode gradient;
        public bool bold;
        public bool folded;
        public List<MemoPage> pages = new List<MemoPage>();
        public int current;

        // before pages existed a memo had one text; moved into pages[0] the first time it's used
        public bool twoColumns;
        public string text = "";
        public string text2 = "";

        /// <summary>The page being shown.</summary>
        public MemoPage Page
        {
            get
            {
                if (pages.Count == 0)
                {
                    pages.Add(new MemoPage { text = text ?? "", text2 = text2 ?? "", twoColumns = twoColumns, modified = DateTime.Now.Ticks });
                    text = ""; text2 = ""; twoColumns = false;
                }
                current = Mathf.Clamp(current, 0, pages.Count - 1);
                return pages[current];
            }
        }
    }

    [Serializable]
    public class MemoPage
    {
        public string text = "";
        public string text2 = "";
        public bool twoColumns;
        public long modified; // DateTime ticks
    }

    // ====================================================================== window

    /// <summary>
    /// Memo Desk: a dockable tab. Drop GameObjects from the Hierarchy to pin a memo to them.
    /// Each memo = object bar (same look as the Hierarchy row) + note (light markdown, 1 or 2 columns).
    /// Click a bar to select the object; drag a bar to reorder; the active bar shows a remove strip.
    /// </summary>
    public class EHMemoDesk : EditorWindow
    {
        VisualElement _list, _empty, _dropHint;
        Label _count;
        Button _foldAll, _deleteMode;
        internal bool DeleteMode;
        internal static VisualElement MenuLayer; // window root: floating menus go here
        internal static ScrollView ScrollRef;    // the list's scroll view (memos follow the caret)
        readonly Dictionary<string, GameObject> _resolved = new Dictionary<string, GameObject>();
        readonly List<EHMemoCard> _cards = new List<EHMemoCard>();
        internal bool Reordering;
        bool _barsDirty;

        [MenuItem("Tools/Empathy Hierarchy/Memo Desk", false, 21)]
        public static void Open()
        {
            var w = GetWindow<EHMemoDesk>();
            w.titleContent = new GUIContent("Memo Desk", EditorGUIUtility.IconContent("TextAsset Icon").image);
            w.minSize = new Vector2(240f, 160f);
            w.Show();
        }

        void OnEnable()
        {
            EditorApplication.delayCall += SweepImages;
            EHStore.Changed += MarkBarsDirty;
            EditorApplication.hierarchyChanged += MarkBarsDirty;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosed += OnSceneClosed;
        }

        void OnDisable()
        {
            EHStore.Changed -= MarkBarsDirty;
            EditorApplication.hierarchyChanged -= MarkBarsDirty;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneClosed -= OnSceneClosed;
        }

        /// <summary>Deletes memo pictures that no memo page refers to any more.</summary>
        internal static void SweepImages()
        {
            var texts = new List<string>();
            foreach (var e in EHMemoStore.instance.entries)
            {
                texts.Add(e.text); texts.Add(e.text2);
                foreach (var p in e.pages) { texts.Add(p.text); texts.Add(p.text2); }
            }
            EHMemoImages.Sweep(texts);
        }

        void OnSceneOpened(UnityEngine.SceneManagement.Scene s, OpenSceneMode m) { _resolved.Clear(); MarkBarsDirty(); }
        void OnSceneClosed(UnityEngine.SceneManagement.Scene s) { _resolved.Clear(); MarkBarsDirty(); }

        // bars are refreshed at most once per editor tick, only when something changed
        void MarkBarsDirty()
        {
            if (_barsDirty || rootVisualElement == null) return;
            _barsDirty = true;
            rootVisualElement.schedule.Execute(() =>
            {
                _barsDirty = false;
                foreach (var c in _cards) c.RefreshBar();
            });
        }

        void CreateGUI()
        {
            var root = rootVisualElement;
            MenuLayer = root;
            root.style.backgroundColor = EHTheme.Bg;
            root.style.paddingLeft = 8; root.style.paddingRight = 8; root.style.paddingTop = 6;

            // header
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.height = 26;
            header.style.marginBottom = 4;
            var title = new Label("MEMO DESK");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 11;
            title.style.color = EHTheme.Dim;
            header.Add(title);
            _count = new Label();
            _count.style.fontSize = 10;
            _count.style.color = EHTheme.Dim;
            _count.style.marginLeft = 6;
            _count.style.minWidth = 18;
            header.Add(_count);
            // text size for every memo (next to the title, so the buttons on the right can't push it around)
            var sizeIcon = new Label("Aa") { tooltip = "Memo text size" };
            sizeIcon.style.color = EHTheme.Dim;
            sizeIcon.style.fontSize = 11;
            sizeIcon.style.marginLeft = 6;
            sizeIcon.style.marginRight = 2;
            header.Add(sizeIcon);
            // 10 steps (80% … 170%), applied right away while dragging
            var size = new SliderInt(0, 9) { value = Mathf.RoundToInt((MemoColumn.Scale - 0.8f) * 10f), tooltip = "Memo text size" };
            size.style.width = 90;
            size.style.marginRight = 8;
            size.RegisterValueChangedCallback(e =>
            {
                if (e.newValue == e.previousValue) return;
                MemoColumn.Scale = 0.8f + e.newValue * 0.1f;
                Rebuild();
            });
            header.Add(size);
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            header.Add(spacer);
            _deleteMode = MemoUI.SmallButton("Delete Mode", "Delete mode: click an object bar to show its remove button", ToggleDeleteMode);
            _deleteMode.style.marginRight = 4;
            _deleteMode.style.width = 110;
            header.Add(_deleteMode);
            _foldAll = MemoUI.SmallButton("Fold all", "Fold or unfold every memo", ToggleFoldAll);
            _foldAll.style.width = 80;
            header.Add(_foldAll);
            root.Add(header);

            // divider: a little distance between the header and the first memo
            var divider = new VisualElement();
            divider.style.height = 1;
            divider.style.backgroundColor = EHTheme.Divider;
            divider.style.marginBottom = 8;
            root.Add(divider);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            ScrollRef = scroll;
            root.Add(scroll);
            _list = new VisualElement();
            _list.style.paddingBottom = 12;
            scroll.Add(_list);

            _empty = MemoUI.Hint("Drag a GameObject from the Hierarchy here\nto pin a memo to it.");
            root.Add(_empty);

            // drop highlight over the whole window
            _dropHint = new VisualElement { pickingMode = PickingMode.Ignore };
            _dropHint.style.position = Position.Absolute;
            _dropHint.style.left = 4; _dropHint.style.right = 4; _dropHint.style.top = 4; _dropHint.style.bottom = 4;
            MemoUI.Radius(_dropHint, 8);
            MemoUI.Border(_dropHint, EHTheme.Point, 2f);
            _dropHint.style.backgroundColor = EHDraw.WithAlpha(EHTheme.Point, 0.06f);
            _dropHint.style.display = DisplayStyle.None;
            root.Add(_dropHint);

            root.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            root.RegisterCallback<DragPerformEvent>(OnDragPerform);
            root.RegisterCallback<DragLeaveEvent>(_ => _dropHint.style.display = DisplayStyle.None);
            root.RegisterCallback<DragExitedEvent>(_ => _dropHint.style.display = DisplayStyle.None);

            Rebuild();
        }

        // ------------------------------------------------------------------ list

        internal void Rebuild()
        {
            if (_list == null) return;
            _pagesMenu?.Close();
            _list.Clear();
            _cards.Clear();
            var entries = EHMemoStore.instance.entries;
            foreach (var entry in entries)
            {
                var card = new EHMemoCard(this, entry);
                _cards.Add(card);
                _list.Add(card);
            }
            _empty.style.display = entries.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _count.text = entries.Count > 0 ? entries.Count.ToString() : "";
            UpdateFoldAllLabel();
        }

        internal GameObject Resolve(MemoEntry e)
        {
            if (_resolved.TryGetValue(e.id, out var go) && go != null) return go;
            go = null;
            if (GlobalObjectId.TryParse(e.id, out var gid))
                go = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid) as GameObject;
            if (go != null) _resolved[e.id] = go;
            else _resolved.Remove(e.id);
            return go;
        }


        void ToggleDeleteMode()
        {
            DeleteMode = !DeleteMode;
            _deleteMode.text = DeleteMode ? "Delete Mode On" : "Delete Mode";
            _deleteMode.style.backgroundColor = DeleteMode ? MemoUI.Danger : EHTheme.Card;
            _deleteMode.style.color = DeleteMode ? Color.white : EHTheme.Text;
            _deleteMode.userData = DeleteMode ? (object)true : null; // keeps the red while hovering
            foreach (var c in _cards) c.RefreshDeleteMode();
            _pagesMenu?.Close();
        }

        internal void Remove(EHMemoCard card)
        {
            var store = EHMemoStore.instance;
            store.entries.Remove(card.Entry);
            store.SaveNow();
            SweepImages(); // its pictures go with it
            Rebuild();
        }

        internal static void Save() => EHMemoStore.instance.SaveNow();

        PagesMenu _pagesMenu;

        /// <summary>Opens the page list under a memo's page chip.</summary>
        internal void ShowPages(EHMemoCard card, VisualElement anchor)
        {
            if (_pagesMenu == null) _pagesMenu = new PagesMenu();
            if (_pagesMenu.parent == rootVisualElement && _pagesMenu.Card == card) { _pagesMenu.Close(); return; }
            _pagesMenu.Open(card, rootVisualElement, anchor, rootVisualElement.WorldToLocal(anchor.worldBound), DeleteMode);
        }

        void ToggleFoldAll()
        {
            var entries = EHMemoStore.instance.entries;
            bool anyOpen = false;
            foreach (var e in entries) if (!e.folded) { anyOpen = true; break; }
            foreach (var e in entries) e.folded = anyOpen;
            Save();
            foreach (var c in _cards) c.RefreshFold();
            UpdateFoldAllLabel();
        }

        internal void UpdateFoldAllLabel()
        {
            if (_foldAll == null) return;
            bool anyOpen = false;
            foreach (var e in EHMemoStore.instance.entries) if (!e.folded) { anyOpen = true; break; }
            _foldAll.text = anyOpen ? "Fold all" : "Unfold all";
        }

        // ------------------------------------------------------------------ reorder (drag a bar)

        internal void DragTo(EHMemoCard card, float worldY)
        {
            int from = _cards.IndexOf(card);
            if (from < 0) return;
            int to = from;
            for (int i = 0; i < _cards.Count; i++)
            {
                if (i == from) continue;
                var b = _cards[i].worldBound;
                if (i < from && worldY < b.center.y) { to = i; break; }
                if (i > from && worldY > b.center.y) to = i;
            }
            if (to == from) return;
            Reordering = true;
            var entries = EHMemoStore.instance.entries;
            // move the neighbours, never the dragged card itself (that would drop its pointer capture)
            while (from != to)
            {
                int n = to < from ? from - 1 : from + 1;
                var neighbour = _cards[n];
                neighbour.RemoveFromHierarchy();
                _list.Insert(from, neighbour);
                _cards[from] = neighbour;
                _cards[n] = card;
                var e = entries[from];
                entries[from] = entries[n];
                entries[n] = e;
                from = n;
            }
        }

        internal void EndDrag()
        {
            Save();
            rootVisualElement.schedule.Execute(() => Reordering = false).StartingIn(250);
        }

        // ------------------------------------------------------------------ drag & drop from the Hierarchy

        static bool IsSceneObject(UnityEngine.Object o) => o is GameObject g && !EditorUtility.IsPersistent(g);

        void OnDragUpdated(DragUpdatedEvent e)
        {
            bool any = false;
            foreach (var o in DragAndDrop.objectReferences) if (IsSceneObject(o)) { any = true; break; }
            DragAndDrop.visualMode = any ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
            _dropHint.style.display = any ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void OnDragPerform(DragPerformEvent e)
        {
            _dropHint.style.display = DisplayStyle.None;
            DragAndDrop.AcceptDrag();
            var store = EHMemoStore.instance;
            int added = 0;
            string unsaved = null;
            foreach (var o in DragAndDrop.objectReferences)
            {
                if (!IsSceneObject(o)) continue;
                var go = (GameObject)o;
                if (string.IsNullOrEmpty(go.scene.path)) { unsaved = go.name; continue; }
                string id = GlobalObjectId.GetGlobalObjectIdSlow(go).ToString();
                if (store.entries.Exists(x => x.id == id)) continue;
                var entry = new MemoEntry { id = id };
                EHMemoCard.Snapshot(entry, go);
                store.entries.Add(entry);
                _resolved[id] = go;
                added++;
            }
            if (added > 0)
            {
                store.SaveNow();
                Rebuild();
            }
            if (unsaved != null) ShowNotification(new GUIContent("Save the scene first  (" + unsaved + ")"), 1.6);
        }
    }

    // ====================================================================== one memo

    class EHMemoCard : VisualElement
    {
        public readonly MemoEntry Entry;
        readonly EHMemoDesk _desk;
        readonly VisualElement _strip, _bar, _clip, _body, _columns;
        readonly Image _icon;
        readonly Label _name, _split, _pages;
        MemoColumn _col1, _col2;

        bool _pressed, _dragging;
        float _bodyHeight;
        bool _opening;
        Vector2 _downPos;
        int _pointerId;

        public EHMemoCard(EHMemoDesk desk, MemoEntry entry)
        {
            _desk = desk;
            Entry = entry;
            style.marginBottom = 8;
            EHSlide.Attach(this, () => _desk.Reordering);

            // remove strip: only in Delete Mode, and it still asks before deleting
            _strip = new VisualElement();
            _strip.style.flexDirection = FlexDirection.Row;
            _strip.style.justifyContent = Justify.FlexEnd;
            _strip.style.alignItems = Align.Center;
            _strip.style.marginBottom = 3;
            BuildStrip(false);
            Add(_strip);

            // object bar
            _bar = new VisualElement { tooltip = "Click: fold / unfold  ·  Icon: select the object  ·  Drag: reorder" };
            _bar.style.flexDirection = FlexDirection.Row;
            _bar.style.alignItems = Align.Center;
            _bar.style.height = 24;
            _bar.style.paddingLeft = 6; _bar.style.paddingRight = 2;
            MemoUI.Radius(_bar, 6);
            _icon = new Image { scaleMode = ScaleMode.ScaleToFit, tooltip = "Select the object" };
            _icon.style.width = 16; _icon.style.height = 16; _icon.style.marginRight = 6; _icon.style.flexShrink = 0;
            _icon.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                e.StopPropagation();
                var go = _desk.Resolve(Entry);
                if (go == null) return;
                Selection.activeGameObject = go;
                EditorGUIUtility.PingObject(go);
            });
            _bar.Add(_icon);
            _name = new Label { pickingMode = PickingMode.Ignore };
            _name.style.flexGrow = 1;
            _name.style.fontSize = 12;
            _name.style.unityTextAlign = TextAnchor.MiddleLeft;
            _name.style.overflow = Overflow.Hidden;
            _name.style.textOverflow = TextOverflow.Ellipsis;
            _name.style.whiteSpace = WhiteSpace.NoWrap;
            _bar.Add(_name);

            // pages: "2 / 3" opens the list of this object's memo pages
            _pages = new Label { tooltip = "Memo pages: switch, add a new one" + "\n(Delete Mode: delete pages)" };
            _pages.style.fontSize = 10;
            _pages.style.unityFontStyleAndWeight = FontStyle.Bold;
            _pages.style.unityTextAlign = TextAnchor.MiddleCenter;
            _pages.style.height = 18;
            _pages.style.minWidth = 34;
            _pages.style.paddingLeft = 6; _pages.style.paddingRight = 6;
            _pages.style.marginRight = 2;
            _pages.style.flexShrink = 0;
            MemoUI.Radius(_pages, 9);
            _pages.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                e.StopPropagation(); // no fold
                _desk.ShowPages(this, _pages);
            });
            _pages.RegisterCallback<PointerEnterEvent>(_ => _pages.style.backgroundColor = EHDraw.WithAlpha(Color.black, 0.32f));
            _pages.RegisterCallback<PointerLeaveEvent>(_ => _pages.style.backgroundColor = EHDraw.WithAlpha(Color.black, 0.18f));
            _bar.Add(_pages);
            _bar.RegisterCallback<PointerDownEvent>(OnBarDown);
            _bar.RegisterCallback<PointerMoveEvent>(OnBarMove);
            _bar.RegisterCallback<PointerUpEvent>(OnBarUp);
            Add(_bar);

            // memo body: 1 or 2 columns + a tall +/− on the right. _clip animates the fold.
            _clip = new VisualElement();
            _clip.style.overflow = Overflow.Hidden;
            _body = new VisualElement();
            _body.style.flexShrink = 0; // keeps its natural height inside the animated clip
            _body.style.flexDirection = FlexDirection.Row;
            _body.style.marginTop = 4;
            _columns = new VisualElement();
            _columns.style.flexDirection = FlexDirection.Row;
            _columns.style.flexGrow = 1;
            _body.Add(_columns);
            _split = new Label();
            _split.style.width = 16;
            _split.style.marginLeft = 4;
            _split.style.unityTextAlign = TextAnchor.MiddleCenter;
            _split.style.fontSize = 13;
            _split.style.color = EHTheme.Dim;
            _split.style.backgroundColor = EHTheme.GroupBg;
            MemoUI.Radius(_split, 5);
            _split.RegisterCallback<PointerEnterEvent>(_ => { if (!Entry.Page.twoColumns) _split.style.backgroundColor = EHTheme.CardHover; });
            _split.RegisterCallback<PointerLeaveEvent>(_ => { if (!Entry.Page.twoColumns) _split.style.backgroundColor = EHTheme.GroupBg; });
            _split.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                if (Entry.Page.twoColumns) AskMerge(); // "−" asks first, like the other deletes
                else ToggleColumns();
            });
            _body.Add(_split);
            _clip.Add(_body);
            Add(_clip);
            _clip.RegisterCallback<TransitionEndEvent>(OnFoldAnimEnd);
            _body.RegisterCallback<GeometryChangedEvent>(e =>
            {
                if (e.newRect.height <= 4f) return;
                _bodyHeight = e.newRect.height;
                // text fields finish measuring a moment later: keep the opening slide heading for the real height
                if (_opening) _clip.style.height = _bodyHeight + 4f;
            });

            BuildColumns();
            RefreshBar();
            ApplyFold(false);
            RefreshDeleteMode();
        }

        // ------------------------------------------------------------------ look

        /// <summary>Remember the object's name and look for when its scene isn't open.</summary>
        internal static void Snapshot(MemoEntry e, GameObject go)
        {
            e.name = go.name;
            var st = EHStore.Get(go);
            e.hasColor = st != null && st.hasColor;
            if (e.hasColor)
            {
                e.color = EHDraw.Opaque(st.Color1);
                e.color2 = EHDraw.Opaque(st.Color2);
                e.fill = st.fill;
                e.gradient = st.gradient;
            }
            e.bold = st != null && st.bold;
        }

        public void RefreshBar()
        {
            var go = _desk.Resolve(Entry);
            bool missing = go == null;
            if (!missing)
            {
                string oldName = Entry.name;
                bool oldColor = Entry.hasColor;
                Color oldC = Entry.color;
                Snapshot(Entry, go);
                if (oldName != Entry.name || oldColor != Entry.hasColor || oldC != Entry.color) EHMemoDesk.Save();
            }

            Color bg = EHTheme.Card, text = EHTheme.Text;
            Texture2D grad = null;
            if (Entry.hasColor)
            {
                Color under = EHTheme.GroupBg;
                Color fa = EHHierarchyGUI.FillColor(Entry.color, Entry.fill, under);
                bg = fa;
                if (Entry.gradient != GradientMode.None && Entry.fill != FillMode.Outline)
                {
                    Color fb = Entry.gradient == GradientMode.TwoColor
                        ? EHHierarchyGUI.FillColor(Entry.color2, Entry.fill, under)
                        : under;
                    grad = EHDraw.Gradient(fa, fb);
                }
                text = Entry.fill == FillMode.Solid ? EHDraw.ContrastText(fa) : EHTheme.Text;
                MemoUI.Border(_bar, Entry.fill == FillMode.Outline ? Entry.color : Color.clear, Entry.fill == FillMode.Outline ? 1f : 0f);
            }
            else MemoUI.Border(_bar, Color.clear, 0f);

            _bar.style.backgroundColor = bg;
            _bar.style.backgroundImage = grad != null ? new StyleBackground(grad) : new StyleBackground(StyleKeyword.None);
            _name.style.color = text;
            _pages.style.color = text;
            _pages.style.backgroundColor = EHDraw.WithAlpha(Color.black, 0.18f);
            _pages.text = "▤  " + (Entry.current + 1) + " / " + Math.Max(1, Entry.pages.Count);
            _name.style.unityFontStyleAndWeight = Entry.bold ? FontStyle.Bold : FontStyle.Normal;
            _name.text = missing ? Entry.name + "   (scene not open)" : Entry.name;
            _bar.style.opacity = missing ? 0.6f : 1f;

            Texture tex = null;
            ObjectStyle st = null;
            if (!missing)
            {
                st = EHStore.Get(go);
                tex = EHIcons.Resolve(go, st, EHSettings.instance.componentAwareIcons);
            }
            if (tex == null) tex = EditorGUIUtility.IconContent("GameObject Icon").image;
            _icon.image = tex;
            _icon.tintColor = st != null ? st.IconTint : Color.white;
        }

        public void RefreshFold() => ApplyFold(true);

        static readonly StyleList<StylePropertyName> HeightProp =
            new StyleList<StylePropertyName>(new List<StylePropertyName> { new StylePropertyName("height") });
        static readonly StyleList<TimeValue> NoTime =
            new StyleList<TimeValue>(new List<TimeValue> { new TimeValue(0f, TimeUnit.Millisecond) });
        static readonly StyleList<TimeValue> FoldTime =
            new StyleList<TimeValue>(new List<TimeValue> { new TimeValue(180f, TimeUnit.Millisecond) });
        static readonly StyleList<EasingFunction> FoldEase =
            new StyleList<EasingFunction>(new List<EasingFunction> { new EasingFunction(EasingMode.EaseOutCubic) });

        /// <summary>Slides the memo open / shut (height 0 ↔ its natural height, then back to auto).</summary>
        void ApplyFold(bool animate)
        {
            _clip.style.transitionProperty = HeightProp;
            _clip.style.transitionTimingFunction = FoldEase;
            if (!animate)
            {
                _clip.style.transitionDuration = NoTime;
                _clip.style.height = Entry.folded ? new StyleLength(0f) : new StyleLength(StyleKeyword.Auto);
                _clip.style.display = Entry.folded ? DisplayStyle.None : DisplayStyle.Flex;
                return;
            }
            _opening = false;
            if (Entry.folded)
            {
                float from = _clip.layout.height;
                _clip.style.transitionDuration = NoTime;
                _clip.style.height = float.IsNaN(from) ? 0f : from;
                _clip.schedule.Execute(() =>
                {
                    _clip.style.transitionDuration = FoldTime;
                    _clip.style.height = 0f;
                }).StartingIn(16);
            }
            else
            {
                _clip.style.display = DisplayStyle.Flex;
                _clip.style.transitionDuration = NoTime;
                _clip.style.height = 0f;
                // wait until the body has been laid out at its real height, then grow to exactly that
                int tries = 0;
                IVisualElementScheduledItem grow = null;
                grow = _clip.schedule.Execute(() =>
                {
                    float h = _body.layout.height;
                    if ((float.IsNaN(h) || h < 4f) && _bodyHeight > 4f) h = _bodyHeight;
                    if ((float.IsNaN(h) || h < 4f) && ++tries < 10) return; // not measured yet
                    grow.Pause();
                    float target = (float.IsNaN(h) || h < 4f ? 60f : h) + 4f;
                    _opening = true;
                    _clip.style.transitionDuration = FoldTime;
                    _clip.style.height = target;
                }).Every(16).StartingIn(16);
            }
        }

        void OnFoldAnimEnd(TransitionEndEvent e)
        {
            if (Entry.folded) _clip.style.display = DisplayStyle.None;
            else
            {
                if (_opening && Mathf.Abs(_clip.resolvedStyle.height - (_body.layout.height + 4f)) > 1f)
                {
                    _clip.style.height = _body.layout.height + 4f; // body grew during the slide: glide on
                    return;
                }
                _opening = false;
                // back to auto so the memo grows while typing
                _clip.style.transitionDuration = NoTime;
                _clip.style.height = StyleKeyword.Auto;
            }
        }

        /// <summary>Remove button, or (after it's clicked) "Delete this memo?" with Delete / Cancel right there.</summary>
        void BuildStrip(bool confirm)
        {
            _strip.Clear();
            if (!confirm)
            {
                _strip.Add(MemoUI.DangerButton("×  Remove memo", "Delete this memo", () => BuildStrip(true)));
                return;
            }
            _strip.Add(MemoUI.ConfirmLabel("Delete this memo and all its pages?"));
            var cancel = MemoUI.SmallButton("Cancel", "Keep it", () => BuildStrip(false));
            cancel.style.marginLeft = 6;
            _strip.Add(cancel);
            var del = MemoUI.DangerButton("Delete", "Delete permanently", () => _desk.Remove(this));
            del.style.marginLeft = 4;
            _strip.Add(del);
        }

        /// <summary>"Back to one column?" with Cancel / Merge in the delete strip.</summary>
        void AskMerge()
        {
            _strip.Clear();
            bool hasText = !string.IsNullOrEmpty(Entry.Page.text2);
            _strip.Add(MemoUI.ConfirmLabel(hasText
                ? "Back to one column? The right column's text moves below the left."
                : "Back to one column?"));
            var cancel = MemoUI.SmallButton("Cancel", "Keep two columns", () => BuildStrip(false));
            cancel.style.marginLeft = 6;
            _strip.Add(cancel);
            var merge = MemoUI.DangerButton("Merge", "Back to one column", () => { BuildStrip(false); ToggleColumns(); });
            merge.style.marginLeft = 4;
            _strip.Add(merge);
        }

        public void RefreshDeleteMode()
        {
            BuildStrip(false);
            _strip.style.display = _desk.DeleteMode ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshSplit();
        }

        /// <summary>"+" adds a column any time; "−" (merge back) only shows in Delete Mode.</summary>
        void RefreshSplit()
        {
            if (Entry.Page.twoColumns)
            {
                _split.text = "−";
                _split.tooltip = "Back to one column (the right text is added below the left)";
                _split.style.display = _desk.DeleteMode ? DisplayStyle.Flex : DisplayStyle.None;
                _split.style.backgroundColor = MemoUI.Danger;
                _split.style.color = Color.white;
                _split.style.unityFontStyleAndWeight = FontStyle.Bold;
            }
            else
            {
                _split.text = "+";
                _split.tooltip = "Split into two columns";
                _split.style.display = DisplayStyle.Flex;
                _split.style.backgroundColor = EHTheme.GroupBg;
                _split.style.color = EHTheme.Dim;
                _split.style.unityFontStyleAndWeight = FontStyle.Normal;
            }
        }

        void BuildColumns()
        {
            _columns.Clear();
            _col1 = new MemoColumn(() => Entry.Page.text, v => { Entry.Page.text = v; Entry.Page.modified = DateTime.Now.Ticks; });
            _columns.Add(_col1);
            _col2 = null;
            if (Entry.Page.twoColumns)
            {
                _col1.style.marginRight = 4;
                _col2 = new MemoColumn(() => Entry.Page.text2, v => { Entry.Page.text2 = v; Entry.Page.modified = DateTime.Now.Ticks; });
                _columns.Add(_col2);
            }
            RefreshSplit();
        }

        /// <summary>Show another page (unfolds the memo if needed).</summary>
        internal void SwitchPage(int index, bool focus)
        {
            Entry.current = Mathf.Clamp(index, 0, Entry.pages.Count - 1);
            EHMemoDesk.Save();
            BuildColumns();
            RefreshBar();
            if (Entry.folded)
            {
                Entry.folded = false;
                RefreshFold();
                _desk.UpdateFoldAllLabel();
            }
            if (focus) _col1.FocusStart();
        }

        internal void NewPage()
        {
            _ = Entry.Page; // move a pre-pages memo into pages first
            Entry.pages.Add(new MemoPage { modified = DateTime.Now.Ticks });
            SwitchPage(Entry.pages.Count - 1, true);
        }

        internal void DeletePage(int index)
        {
            if (Entry.pages.Count <= 1 || index < 0 || index >= Entry.pages.Count) return;
            Entry.pages.RemoveAt(index);
            if (Entry.current >= index && Entry.current > 0) Entry.current--;
            SwitchPage(Entry.current, false);
            EHMemoDesk.SweepImages(); // its pictures go with it
        }

        void ToggleColumns()
        {
            if (Entry.Page.twoColumns && !string.IsNullOrEmpty(Entry.Page.text2))
            {
                Entry.Page.text = string.IsNullOrEmpty(Entry.Page.text) ? Entry.Page.text2 : Entry.Page.text + "\n\n" + Entry.Page.text2;
                Entry.Page.text2 = "";
            }
            Entry.Page.twoColumns = !Entry.Page.twoColumns;
            EHMemoDesk.Save();
            BuildColumns();
        }

        // ------------------------------------------------------------------ bar input

        void OnBarDown(PointerDownEvent e)
        {
            if (e.button != 0) return;
            _pressed = true;
            _dragging = false;
            _downPos = e.position;
            _pointerId = e.pointerId;
            _bar.CapturePointer(_pointerId);
            e.StopPropagation();
        }

        void OnBarMove(PointerMoveEvent e)
        {
            if (!_pressed || !_bar.HasPointerCapture(_pointerId)) return;
            if (!_dragging && (((Vector2)e.position) - _downPos).sqrMagnitude > 25f)
            {
                _dragging = true;
                style.opacity = 0.75f;
            }
            if (_dragging) _desk.DragTo(this, e.position.y);
        }

        void OnBarUp(PointerUpEvent e)
        {
            if (!_pressed) return;
            _pressed = false;
            if (_bar.HasPointerCapture(_pointerId)) _bar.ReleasePointer(_pointerId);
            style.opacity = 1f;
            if (_dragging)
            {
                _dragging = false;
                _desk.EndDrag();
                return;
            }
            Entry.folded = !Entry.folded;
            EHMemoDesk.Save();
            RefreshFold();
            _desk.UpdateFoldAllLabel();
        }
    }

    // ====================================================================== memo text (blocks, styled while typing)

    /// <summary>
    /// One memo column made of blocks, each a Unity text field styled as a whole, so the look is there
    /// while typing: Title / Subtitle (bigger, bold, thin line under), Quote (bar on the left), Code
    /// (monospace box), Divider. Bullets and checkboxes stay inside text blocks so several can be selected
    /// and deleted at once. Markdown shortcuts and the "/" menu create them.
    /// </summary>
    class MemoColumn : VisualElement
    {
        readonly Func<string> _get;
        readonly Action<string> _set;
        readonly MemoDoc _doc;
        readonly VisualElement _blocks;
        readonly List<TextField> _fields = new List<TextField>(); // index = block index (null for dividers)
        IVisualElementScheduledItem _saveLater;

        // "/" menu
        SlashMenu _menu;
        bool _menuOpen, _expectSlash, _swallowNewline;
        int _menuBlock, _slashPos = -1, _selA, _selB;

        const string ScalePref = "EmpathyHierarchy.MemoTextScale";
        static float _scale = -1f;
        /// <summary>Text size for every memo (slider in the Memo Desk header). Personal, not shared.</summary>
        internal static float Scale
        {
            get
            {
                if (_scale < 0f) _scale = Mathf.Clamp(EditorPrefs.GetFloat(ScalePref, 1f), 0.8f, 1.7f);
                return _scale;
            }
            set
            {
                _scale = Mathf.Clamp(value, 0.8f, 1.7f);
                EditorPrefs.SetFloat(ScalePref, _scale);
            }
        }

        static int Px(float size) => Mathf.RoundToInt(size * Scale);

        static readonly Color QuoteText = new Color(0.86f, 0.81f, 0.75f);
        static readonly Color TitleText = new Color(0.96f, 0.93f, 0.89f);

        static Font _mono;
        static Font Mono
        {
            get
            {
                if (_mono == null)
                    _mono = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Menlo", "Cascadia Mono", "Courier New" }, 12);
                return _mono;
            }
        }

        public MemoColumn(Func<string> get, Action<string> set)
        {
            _get = get;
            _set = set;
            _doc = MemoDoc.FromMarkdown(get() ?? "");
            _lastMd = _doc.ToMarkdown();
            style.flexGrow = 1;

            // Ctrl/Cmd+Z, Ctrl+Y, Ctrl/Cmd+Shift+Z: the memo's own undo, before the field or Unity sees the key
            RegisterCallback<KeyDownEvent>(e =>
            {
                if (!e.actionKey) return;
                bool undo = e.keyCode == KeyCode.Z && !e.shiftKey;
                bool redo = e.keyCode == KeyCode.Y || (e.keyCode == KeyCode.Z && e.shiftKey);
                if (!undo && !redo) return;
                e.StopImmediatePropagation();
                CloseMenu();
                UndoRedo(redo);
            }, TrickleDown.TrickleDown);
            RegisterCallback<ValidateCommandEvent>(e =>
            {
                if (e.commandName == "Undo" || e.commandName == "Redo") e.StopPropagation();
            });
            RegisterCallback<ExecuteCommandEvent>(e =>
            {
                if (e.commandName != "Undo" && e.commandName != "Redo") return;
                e.StopPropagation();
                UndoRedo(e.commandName == "Redo");
            });
            style.flexBasis = 0;
            style.minHeight = 40;
            style.backgroundColor = EHTheme.Card;
            style.paddingLeft = 8; style.paddingRight = 8; style.paddingTop = 6; style.paddingBottom = 8;
            MemoUI.Radius(this, 6);

            _blocks = new VisualElement();
            Add(_blocks);

            // picture files dragged in (Explorer / Finder / Project window) go to the end of the memo
            RegisterCallback<DragUpdatedEvent>(e =>
            {
                if (!DraggedImages(out _)) return;
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                e.StopPropagation();
            });
            RegisterCallback<DragPerformEvent>(e =>
            {
                if (!DraggedImages(out var files)) return;
                DragAndDrop.AcceptDrag();
                e.StopPropagation();
                foreach (var file in files)
                {
                    int last = _doc.Blocks.Count - 1;
                    InsertImage(last, _doc.Blocks[last].text.Length, EHMemoImages.Import(file));
                }
            });

            // a click on empty space in the memo continues at the end
            RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0 || (e.target != this && e.target != _blocks)) return;
                int last = _doc.Blocks.Count - 1;
                if (_doc.Blocks[last].kind != MemoDoc.Kind.Text)
                {
                    _doc.Blocks.Add(new MemoDoc.Block(MemoDoc.Kind.Text));
                    last++;
                    Rebuild(last, 0);
                    return;
                }
                Focus(last, _doc.Blocks[last].text.Length);
            });

            Rebuild(-1, 0);
        }

        // ------------------------------------------------------------------ building

        void Rebuild(int focusBlock, int focusPos)
        {
            CloseMenu();
            _blocks.Clear();
            _fields.Clear();
            _viewOf.Clear();
            _renderOf.Clear();
            for (int i = 0; i < _doc.Blocks.Count; i++)
            {
                var b = _doc.Blocks[i];
                if (b.kind == MemoDoc.Kind.Divider)
                {
                    _fields.Add(null);
                    _blocks.Add(MakeDivider(i));
                    continue;
                }
                if (b.kind == MemoDoc.Kind.Image)
                {
                    _fields.Add(null);
                    _blocks.Add(MakeImage(i, b));
                    continue;
                }
                var f = MakeField(i, b);
                _fields.Add(f);
                VisualElement el = f;
                if (b.kind == MemoDoc.Kind.Checklist) el = MakeChecklist(f);
                else if (b.kind == MemoDoc.Kind.Code) el = MakeCode(f);
                else if (b.kind == MemoDoc.Kind.Callout) el = MakeCallout(f);
                _blocks.Add(el);
            }
            if (focusBlock >= 0) Focus(focusBlock, focusPos);
        }

        VisualElement MakeDivider(int index)
        {
            var hit = new VisualElement { tooltip = "Divider  (Backspace at the start of the line below removes it)" };
            hit.style.height = 11;
            hit.style.justifyContent = Justify.Center;
            var line = new VisualElement { pickingMode = PickingMode.Ignore };
            line.style.height = 1;
            line.style.backgroundColor = EHTheme.Dim;
            line.style.opacity = 0.45f;
            hit.Add(line);
            return hit;
        }

        const float MaxImageHeight = 420f;

        static bool DraggedImages(out List<string> files)
        {
            files = null;
            var paths = DragAndDrop.paths;
            if (paths == null) return false;
            foreach (var p in paths)
            {
                if (!EHMemoImages.IsImageFile(p) || !System.IO.File.Exists(p)) continue;
                if (files == null) files = new List<string>();
                files.Add(p);
            }
            return files != null;
        }

        /// <summary>
        /// Picture block: fits the column width. Click selects it and shows its bar
        /// (align left / center / right · Copy · ×); Ctrl+C copies, Delete asks to remove, double-click opens it full size.
        /// </summary>
        VisualElement MakeImage(int index, MemoDoc.Block block)
        {
            string id = block.text;
            var box = new VisualElement { focusable = true, tooltip = "Click: select  ·  Double-click: open full size" };
            box.style.marginTop = 4; box.style.marginBottom = 4;
            box.style.minHeight = 30;
            var tex = EHMemoImages.Load(id);

            Image img = null;
            if (tex == null)
            {
                var missing = new Label("Picture not found: " + id) { pickingMode = PickingMode.Ignore };
                missing.style.color = EHTheme.Dim;
                missing.style.fontSize = Px(11);
                missing.style.unityFontStyleAndWeight = FontStyle.Italic;
                missing.style.paddingTop = 6;
                box.Add(missing);
            }
            else
            {
                img = new Image { image = tex, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                MemoUI.Radius(img, 4);
                box.Add(img);
                // size from the column width, keeping the aspect (never wider than the picture itself)
                box.RegisterCallback<GeometryChangedEvent>(_ =>
                {
                    float w = Mathf.Min(box.contentRect.width, tex.width);
                    if (w <= 0f) return;
                    float h = w * tex.height / tex.width;
                    if (h > MaxImageHeight) { w *= MaxImageHeight / h; h = MaxImageHeight; }
                    img.style.width = w;
                    img.style.height = h;
                });
            }
            Action applyAlign = () =>
            {
                var a = block.align == 1 ? Align.Center : block.align == 2 ? Align.FlexEnd : Align.FlexStart;
                if (img != null) img.style.alignSelf = a;
            };
            applyAlign();

            // ---- selection bar (top right), shown while the picture is selected
            var bar = new VisualElement();
            bar.style.position = Position.Absolute;
            bar.style.top = 6; bar.style.right = 6;
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.alignItems = Align.Center;
            bar.style.paddingLeft = 3; bar.style.paddingRight = 3; bar.style.paddingTop = 3; bar.style.paddingBottom = 3;
            bar.style.backgroundColor = new Color(0.11f, 0.11f, 0.11f, 0.92f);
            MemoUI.Radius(bar, 7);
            bar.style.display = DisplayStyle.None;
            box.Add(bar);

            var alignButtons = new List<Button>();
            Action paintAlign = () =>
            {
                for (int i = 0; i < alignButtons.Count; i++)
                {
                    bool on = i == block.align;
                    alignButtons[i].userData = on ? (object)true : null;
                    alignButtons[i].style.backgroundColor = on ? EHTheme.CardHover : Color.clear;
                }
            };
            Action<int> setAlign = a =>
            {
                if (block.align == a) return;
                block.align = a;
                applyAlign();
                paintAlign();
                Commit();
            };
            string[] alignTips = { "Align left", "Align center", "Align right" };
            for (int i = 0; i < 3; i++)
            {
                int a = i;
                var b = MemoUI.SmallButton("", alignTips[i], () => setAlign(a));
                b.Add(AlignGlyph(a));
                BarButton(b);
                alignButtons.Add(b);
                bar.Add(b);
            }
            paintAlign();

            var gap = new VisualElement();
            gap.style.width = 1; gap.style.height = 14;
            gap.style.marginLeft = 4; gap.style.marginRight = 4;
            gap.style.backgroundColor = new Color(1f, 1f, 1f, 0.15f);
            bar.Add(gap);

            double copiedAt = -1;
            Action copy = () =>
            {
                // Ctrl+C arrives as a key and as the editor's Copy command: copy once
                if (EditorApplication.timeSinceStartup - copiedAt < 0.3) return;
                copiedAt = EditorApplication.timeSinceStartup;
                if (EHMemoImages.CopyToClipboard(id)) Notice("Picture copied");
            };
            var copyButton = MemoUI.SmallButton("Copy", "Copy the picture (Ctrl+C)", copy);
            BarButton(copyButton);
            copyButton.style.width = StyleKeyword.Auto;
            copyButton.style.paddingLeft = 7; copyButton.style.paddingRight = 7;
            copyButton.SetEnabled(tex != null);
            bar.Add(copyButton);

            // ---- asking before the file is deleted, in place of the bar
            var ask = new VisualElement();
            ask.style.position = Position.Absolute;
            ask.style.top = 6; ask.style.right = 6;
            ask.style.flexDirection = FlexDirection.Row;
            ask.style.alignItems = Align.Center;
            ask.style.paddingLeft = 8; ask.style.paddingRight = 3; ask.style.paddingTop = 3; ask.style.paddingBottom = 3;
            ask.style.backgroundColor = new Color(0.3f, 0.1f, 0.1f, 0.95f);
            MemoUI.Radius(ask, 7);
            ask.style.display = DisplayStyle.None;
            ask.Add(MemoUI.ConfirmLabel("Delete this picture?"));
            box.Add(ask);

            bool asking = false;
            Action showBar = () => { asking = false; ask.style.display = DisplayStyle.None; bar.style.display = DisplayStyle.Flex; };
            Action askDelete = () => { asking = true; bar.style.display = DisplayStyle.None; ask.style.display = DisplayStyle.Flex; };
            Action delete = () =>
            {
                Commit();
                _doc.RemoveObject(index, out int bi, out int p);
                Restructure(bi, p);
                EHMemoDesk.SweepImages(); // the file goes too (unless another memo still shows it)
            };

            var x = MemoUI.DangerButton("×", "Delete the picture (Delete)", askDelete);
            BarButton(x);
            x.style.marginLeft = 4;
            bar.Add(x);

            var no = MemoUI.SmallButton("Cancel", "Keep it", () => { showBar(); box.Focus(); });
            no.focusable = false;
            no.style.marginLeft = 6;
            ask.Add(no);
            var yes = MemoUI.DangerButton("Delete", "Delete the picture and its file", delete);
            yes.focusable = false;
            yes.style.marginLeft = 4;
            ask.Add(yes);

            // ---- selection
            Color sel = new Color(0.35f, 0.6f, 1f, 0.9f);
            box.RegisterCallback<FocusInEvent>(_ =>
            {
                showBar();
                if (img != null) MemoUI.Border(img, sel, 2f);
            });
            box.RegisterCallback<FocusOutEvent>(_ =>
            {
                asking = false;
                bar.style.display = DisplayStyle.None;
                ask.style.display = DisplayStyle.None;
                if (img != null) MemoUI.Border(img, Color.clear, 0f);
            });
            box.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                if (e.clickCount == 2 && tex != null) EditorUtility.OpenWithDefaultApp(System.IO.Path.GetFullPath(EHMemoImages.PathOf(id)));
                box.Focus();
                e.StopPropagation();
            });
            box.RegisterCallback<ValidateCommandEvent>(e =>
            {
                if (e.commandName == "Copy" || e.commandName == "Delete" || e.commandName == "SoftDelete") { e.StopPropagation(); }
            });
            box.RegisterCallback<ExecuteCommandEvent>(e =>
            {
                if (e.commandName == "Copy") { copy(); e.StopPropagation(); }
                else if (e.commandName == "Delete" || e.commandName == "SoftDelete") { askDelete(); e.StopPropagation(); }
            });
            box.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.actionKey && e.keyCode == KeyCode.C) { copy(); e.StopPropagation(); }
                else if (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace) { askDelete(); e.StopPropagation(); }
                else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    if (asking) delete();
                    e.StopPropagation();
                }
                else if (e.keyCode == KeyCode.Escape)
                {
                    if (asking) showBar();
                    else box.Blur();
                    e.StopPropagation();
                }
            });
            return box;
        }

        /// <summary>Square, non-focusable button for the picture bar (focus stays on the picture).</summary>
        static void BarButton(Button b)
        {
            b.focusable = false;
            b.style.width = 24; b.style.height = 20;
            b.style.paddingLeft = 0; b.style.paddingRight = 0;
            b.style.marginLeft = 1; b.style.marginRight = 1;
            b.style.alignItems = Align.Center;
            b.style.justifyContent = Justify.Center;
        }

        /// <summary>Three short lines lined up left, center or right.</summary>
        static VisualElement AlignGlyph(int align)
        {
            var g = new VisualElement { pickingMode = PickingMode.Ignore };
            g.style.width = 12;
            g.style.alignItems = align == 1 ? Align.Center : align == 2 ? Align.FlexEnd : Align.FlexStart;
            float[] widths = { 12f, 7f, 10f };
            for (int i = 0; i < 3; i++)
            {
                var line = new VisualElement { pickingMode = PickingMode.Ignore };
                line.style.width = widths[i];
                line.style.height = 2;
                line.style.marginTop = i == 0 ? 0 : 2;
                line.style.backgroundColor = EHTheme.Text;
                MemoUI.Radius(line, 1);
                g.Add(line);
            }
            return g;
        }

        /// <summary>Puts a picture below the given caret position and saves.</summary>
        void InsertImage(int block, int pos, string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            Commit();
            block = Mathf.Clamp(block, 0, _doc.Blocks.Count - 1);
            if (MemoDoc.IsObject(_doc.Blocks[block].kind))
            {
                // after an object: add a text line to put it under
                _doc.Blocks.Insert(block + 1, new MemoDoc.Block(MemoDoc.Kind.Text));
                block++;
                pos = 0;
            }
            pos = Mathf.Clamp(pos, 0, _doc.Blocks[block].text.Length);
            _doc.InsertImage(ref block, ref pos, id);
            Restructure(block, pos);
        }

        /// <summary>Ctrl/Cmd+V with a picture (and no text) on the clipboard.</summary>
        bool TryPasteImage(int block, int pos)
        {
            if (!string.IsNullOrEmpty(EditorGUIUtility.systemCopyBuffer)) return false; // text wins: normal paste
            var tex = EHMemoImages.ReadClipboard();
            if (tex == null) return false;
            InsertImage(block, pos, EHMemoImages.Save(tex));
            return true;
        }

        TextField MakeField(int index, MemoDoc.Block b)
        {
            var kind = b.kind;
            var f = new TextField
            {
                multiline = !MemoDoc.SingleLine(kind),
                selectAllOnFocus = false,
                selectAllOnMouseUp = false,
            };
            f.SetValueWithoutNotify(b.text);
            f.style.marginLeft = 0; f.style.marginRight = 0; f.style.marginTop = 0; f.style.marginBottom = 0;

            var input = f.Q(className: TextField.inputUssClassName);
            var te = f.Q<TextElement>();
            if (input != null)
            {
                input.style.backgroundColor = Color.clear;
                MemoUI.Border(input, Color.clear, 0f);
                input.style.paddingLeft = 0; input.style.paddingRight = 0; input.style.paddingTop = 1; input.style.paddingBottom = 1;
                input.style.marginLeft = 0; input.style.marginRight = 0; input.style.marginTop = 0; input.style.marginBottom = 0;
                input.style.color = EHTheme.Text;
            }
            if (te != null && f.multiline) te.style.whiteSpace = WhiteSpace.Normal;

            int size = Px(13);
            var weight = FontStyle.Normal;
            string placeholder = null;
            switch (kind)
            {
                case MemoDoc.Kind.Title:
                    size = Px(19); weight = FontStyle.Bold; placeholder = "Title";
                    f.style.marginTop = 6; f.style.marginBottom = 5; f.style.paddingBottom = 3;
                    f.style.borderBottomWidth = 2; f.style.borderBottomColor = EHDraw.WithAlpha(EHTheme.Point, 0.6f);
                    if (input != null) input.style.color = TitleText;
                    break;
                case MemoDoc.Kind.Subtitle:
                    size = Px(15); weight = FontStyle.Bold; placeholder = "Subtitle";
                    f.style.marginTop = 5; f.style.marginBottom = 4; f.style.paddingBottom = 2;
                    f.style.borderBottomWidth = 1; f.style.borderBottomColor = EHDraw.WithAlpha(EHTheme.Accent, 0.3f);
                    if (input != null) input.style.color = EHTheme.Accent;
                    break;
                case MemoDoc.Kind.Quote:
                    weight = FontStyle.Italic; placeholder = "Quote";
                    f.style.marginTop = 3; f.style.marginBottom = 3;
                    f.style.borderLeftWidth = 3; f.style.borderLeftColor = EHTheme.Point;
                    f.style.backgroundColor = EHDraw.WithAlpha(EHTheme.Point, 0.07f);
                    f.style.paddingLeft = 9; f.style.paddingRight = 6; f.style.paddingTop = 3; f.style.paddingBottom = 3;
                    f.style.borderTopRightRadius = 4; f.style.borderBottomRightRadius = 4;
                    if (input != null) input.style.color = QuoteText;
                    break;
                case MemoDoc.Kind.Checklist:
                    break;
                case MemoDoc.Kind.Callout:
                    weight = FontStyle.Bold; placeholder = "Callout";
                    break;
                case MemoDoc.Kind.Code:
                    size = Px(12); placeholder = "Code";
                    if (Mono != null)
                    {
                        var fd = FontDefinition.FromFont(Mono);
                        f.style.unityFontDefinition = fd;
                        if (input != null) input.style.unityFontDefinition = fd;
                        if (te != null) te.style.unityFontDefinition = fd;
                    }
                    break;
                default:
                    if (_doc.IsEmpty && index == 0) placeholder = "Type / for formatting";
                    break;
            }
            if (input != null) { input.style.fontSize = size; input.style.unityFontStyleAndWeight = weight; }
            if (te != null) { te.style.fontSize = size; te.style.unityFontStyleAndWeight = weight; }
            if (placeholder != null)
            {
                f.textEdition.placeholder = placeholder;
                f.textEdition.hidePlaceholderOnFocus = false;
            }

            f.RegisterValueChangedCallback(e =>
            {
                int i = _fields.IndexOf(f);
                if (i < 0) return;
                _doc.Blocks[i].text = e.newValue ?? "";
                if (_menuOpen)
                {
                    if (_expectSlash) _expectSlash = false;
                    else CloseMenu(); // typing anything else (Korean IME too) closes the menu, text untouched
                }
                Commit();
                KeepVisible(f);
            });
            f.RegisterCallback<KeyDownEvent>(e => OnKey(f, e), TrickleDown.TrickleDown);
            f.RegisterCallback<FocusOutEvent>(_ => schedule.Execute(() =>
            {
                if (_menuOpen && !FieldFocused(f)) CloseMenu();
                EHMemoDesk.Save();
            }).StartingIn(150));
            return f;
        }

        static bool FieldFocused(TextField f)
        {
            var focused = f.panel?.focusController?.focusedElement as VisualElement;
            return focused != null && (focused == f || f.Contains(focused));
        }

        public void FocusStart()
        {
            for (int i = 0; i < _fields.Count; i++)
                if (_fields[i] != null) { Focus(i, 0); return; }
        }

        void Focus(int block, int pos)
        {
            if (block < 0 || block >= _fields.Count) return;
            var f = _fields[block];
            if (f == null)
            {
                // divider: go to the next editable block
                for (int i = block + 1; i < _fields.Count; i++) if (_fields[i] != null) { Focus(i, 0); return; }
                return;
            }
            int p = Mathf.Clamp(pos, 0, (f.value ?? "").Length);
            ShowEditor(f, true);
            f.schedule.Execute(() =>
            {
                f.Focus();
                f.SelectRange(p, p);
            });
        }

        // ------------------------------------------------------------------ undo
        // The memo keeps its own Undo / Redo: Unity's Edit > Undo would undo scene changes instead.
        // Typing is grouped until a short pause; structure changes (Enter, menu, pictures…) are steps of their own.

        class Snap { public string md; public int block, pos; }
        readonly List<Snap> _undo = new List<Snap>(), _redo = new List<Snap>();
        string _lastMd;
        double _lastEdit;
        bool _newStep;
        int _caretBlock, _caretPos;
        const int MaxUndo = 200;
        const double TypingGroup = 0.8; // seconds of pause that start a new step

        void Record(string md)
        {
            double now = EditorApplication.timeSinceStartup;
            if (_newStep || _undo.Count == 0 || now - _lastEdit > TypingGroup)
            {
                _undo.Add(new Snap { md = _lastMd, block = _caretBlock, pos = _caretPos });
                if (_undo.Count > MaxUndo) _undo.RemoveAt(0);
            }
            _newStep = false;
            _redo.Clear();
            _lastMd = md;
            _lastEdit = now;
        }

        void UndoRedo(bool redo)
        {
            var from = redo ? _redo : _undo;
            var to = redo ? _undo : _redo;
            if (from.Count == 0) return;
            var snap = from[from.Count - 1];
            from.RemoveAt(from.Count - 1);
            to.Add(new Snap { md = _doc.ToMarkdown(), block = _caretBlock, pos = _caretPos });

            var restored = MemoDoc.FromMarkdown(snap.md);
            _doc.Blocks.Clear();
            _doc.Blocks.AddRange(restored.Blocks);
            _lastMd = snap.md;
            _lastEdit = 0;
            _set(snap.md);
            if (_saveLater == null) _saveLater = schedule.Execute(EHMemoDesk.Save);
            _saveLater.ExecuteLater(600);

            // caret back where it was (on the nearest text line if that spot is now a picture / divider)
            int b = Mathf.Clamp(snap.block, 0, _doc.Blocks.Count - 1);
            for (int d = 0; d < _doc.Blocks.Count; d++)
            {
                if (b + d < _doc.Blocks.Count && !MemoDoc.IsObject(_doc.Blocks[b + d].kind)) { b += d; break; }
                if (b - d >= 0 && !MemoDoc.IsObject(_doc.Blocks[b - d].kind)) { b -= d; break; }
            }
            int p = MemoDoc.IsObject(_doc.Blocks[b].kind) ? 0 : Mathf.Clamp(snap.pos, 0, _doc.Blocks[b].text.Length);
            _caretBlock = b; _caretPos = p;
            Rebuild(MemoDoc.IsObject(_doc.Blocks[b].kind) ? -1 : b, p);
        }

        void Commit()
        {
            var md = _doc.ToMarkdown();
            if (md != _lastMd) Record(md);
            _set(md);
            if (_saveLater == null) _saveLater = schedule.Execute(EHMemoDesk.Save);
            _saveLater.ExecuteLater(600);
        }

        /// <summary>
        /// Writing at the bottom of a long memo: scroll the desk so the line being typed stays in view.
        /// </summary>
        static void KeepVisible(TextField f)
        {
            var sv = EHMemoDesk.ScrollRef;
            if (sv == null || sv.panel != f.panel) return;
            string v = f.value ?? "";
            int cur = f.cursorIndex;
            if (v.IndexOf('\n', Mathf.Clamp(cur, 0, v.Length)) >= 0) return; // only when typing on the last line
            f.schedule.Execute(() =>
            {
                var vp = sv.contentViewport.worldBound;
                float over = f.worldBound.yMax + 12f - vp.yMax;
                if (over > 0f) sv.scrollOffset = new Vector2(sv.scrollOffset.x, sv.scrollOffset.y + over);
            });
        }

        /// <summary>Block structure changed: save, rebuild, put the caret back.</summary>
        void Restructure(int block, int pos)
        {
            _newStep = true;
            Commit();
            Rebuild(block, pos);
        }

        /// <summary>Text of one block changed in code: update its field and caret.</summary>
        void SetText(TextField f, int block, string text, int a, int b)
        {
            _doc.Blocks[block].text = text;
            f.SetValueWithoutNotify(text);
            Commit();
            KeepVisible(f);
            a = Mathf.Clamp(a, 0, text.Length);
            b = Mathf.Clamp(b, 0, text.Length);
            f.SelectRange(a, b);
            f.schedule.Execute(() =>
            {
                if (!FieldFocused(f)) f.Focus();
                f.SelectRange(a, b);
            });
        }

        // ------------------------------------------------------------------ checklist: real boxes when not typing

        // blocks that show a different "reading" view when not being typed in (checklist boxes, coloured code)
        readonly Dictionary<TextField, VisualElement> _viewOf = new Dictionary<TextField, VisualElement>();
        readonly Dictionary<TextField, Action<TextField>> _renderOf = new Dictionary<TextField, Action<TextField>>();

        void AddViewer(TextField f, VisualElement view, Action<TextField> render)
        {
            _viewOf[f] = view;
            _renderOf[f] = render;
            f.style.display = DisplayStyle.None;
            render(f);
            f.RegisterCallback<FocusOutEvent>(_ => f.schedule.Execute(() =>
            {
                if (!FieldFocused(f)) ShowEditor(f, false);
            }).StartingIn(160));
        }

        void ShowEditor(TextField f, bool edit)
        {
            if (!_viewOf.TryGetValue(f, out var view)) return;
            if (!edit) _renderOf[f](f);
            view.style.display = edit ? DisplayStyle.None : DisplayStyle.Flex;
            f.style.display = edit ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ---- coloured code (colours only in the reading view: a text field can't mix colours)

        static readonly string KeywordHex = "#E3A76F", StringHex = "#A9C98B", CommentHex = "#857A70", NumberHex = "#8FB8DE";

        VisualElement MakeCode(TextField f)
        {
            var box = new VisualElement();
            box.style.marginTop = 3; box.style.marginBottom = 3;
            box.style.backgroundColor = EHTheme.Bg;
            box.style.paddingLeft = 8; box.style.paddingRight = 8; box.style.paddingTop = 6; box.style.paddingBottom = 6;
            MemoUI.Radius(box, 5);
            var view = new Label { enableRichText = true, tooltip = "Click to edit" };
            view.style.whiteSpace = WhiteSpace.Normal;
            view.style.paddingLeft = 0; view.style.paddingRight = 0; view.style.paddingTop = 1; view.style.paddingBottom = 1;
            view.style.marginLeft = 0; view.style.marginRight = 0;
            view.style.fontSize = Px(12);
            view.style.color = EHTheme.Text;
            if (Mono != null) view.style.unityFontDefinition = FontDefinition.FromFont(Mono);
            view.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                e.StopPropagation();
                int i = _fields.IndexOf(f);
                if (i >= 0) Focus(i, int.MaxValue);
            });
            box.Add(view);
            box.Add(f);
            AddViewer(f, view, RenderCode);
            return box;
        }

        void RenderCode(TextField f)
        {
            if (!(_viewOf.TryGetValue(f, out var v) && v is Label label)) return;
            int bi = _fields.IndexOf(f);
            string code = bi >= 0 ? _doc.Blocks[bi].text : f.value ?? "";
            label.text = code.Length == 0
                ? "<color=" + CommentHex + ">Code</color>"
                : MemoDoc.Highlight(code, KeywordHex, StringHex, CommentHex, NumberHex);
        }

        // ---- callout box: plain text (with lists) in a warm box with a mark on the left

        VisualElement MakeCallout(TextField f)
        {
            var box = new VisualElement();
            box.style.flexDirection = FlexDirection.Row;
            box.style.marginTop = 3; box.style.marginBottom = 3;
            box.style.backgroundColor = EHDraw.WithAlpha(EHTheme.Point, 0.10f);
            MemoUI.Border(box, EHDraw.WithAlpha(EHTheme.Point, 0.35f), 1f);
            box.style.paddingLeft = 7; box.style.paddingRight = 8; box.style.paddingTop = 6; box.style.paddingBottom = 6;
            MemoUI.Radius(box, 6);
            var icon = new Label("✦") { pickingMode = PickingMode.Ignore };
            icon.style.color = EHTheme.Point;
            icon.style.fontSize = Px(13);
            icon.style.marginRight = 6; icon.style.marginTop = 1;
            icon.style.paddingLeft = 0; icon.style.paddingRight = 0;
            box.Add(icon);
            f.style.flexGrow = 1;
            f.style.flexShrink = 1;
            box.Add(f);
            return box;
        }

        VisualElement MakeChecklist(TextField f)
        {
            var wrap = new VisualElement();
            wrap.style.marginTop = 2; wrap.style.marginBottom = 2;
            var rows = new VisualElement();
            wrap.Add(rows);
            wrap.Add(f);
            AddViewer(f, rows, RenderChecklist);
            return wrap;
        }

        void RenderChecklist(TextField f)
        {
            if (!_viewOf.TryGetValue(f, out var rows)) return;
            rows.Clear();
            int bi = _fields.IndexOf(f);
            if (bi < 0) return;
            string t = _doc.Blocks[bi].text;
            int start = 0;
            foreach (var line in t.Split('\n'))
            {
                int lineStart = start;
                start += line.Length + 1;
                bool done = line.Length > 0 && line[0] == MemoDoc.DoneGlyph;
                string body = line.Length >= 2 && line[1] == ' ' &&
                              (line[0] == MemoDoc.BoxGlyph || line[0] == MemoDoc.DoneGlyph) ? line.Substring(2) : line;

                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.FlexStart;
                row.style.minHeight = Px(13) + 7;
                row.style.paddingTop = 1; row.style.paddingBottom = 1;

                var box = new Label(done ? "✓" : "") { tooltip = done ? "Mark as not done" : "Mark as done" };
                int bs = Mathf.Max(12, Px(14));
                box.style.width = bs; box.style.height = bs;
                box.style.marginTop = 2; box.style.marginRight = 7; box.style.marginLeft = 1;
                box.style.flexShrink = 0;
                box.style.fontSize = Mathf.Max(9, bs - 4);
                box.style.unityTextAlign = TextAnchor.MiddleCenter;
                box.style.unityFontStyleAndWeight = FontStyle.Bold;
                box.style.paddingLeft = 0; box.style.paddingRight = 0; box.style.paddingTop = 0; box.style.paddingBottom = 0;
                MemoUI.Radius(box, 4);
                if (done) { box.style.backgroundColor = EHTheme.Accent; box.style.color = EHTheme.AccentText; }
                else MemoUI.Border(box, EHTheme.Dim, 1.5f);
                int ls = lineStart;
                box.RegisterCallback<PointerDownEvent>(e =>
                {
                    if (e.button != 0) return;
                    e.StopPropagation();
                    int i = _fields.IndexOf(f);
                    if (i < 0) return;
                    var blk = _doc.Blocks[i];
                    blk.text = MemoDoc.ToggleDoneAt(blk.text, ls);
                    f.SetValueWithoutNotify(blk.text);
                    Commit();
                    RenderChecklist(f);
                });
                box.RegisterCallback<PointerEnterEvent>(_ => { if (!done) box.style.backgroundColor = EHTheme.CardHover; });
                box.RegisterCallback<PointerLeaveEvent>(_ => { if (!done) box.style.backgroundColor = StyleKeyword.Null; });
                row.Add(box);

                var label = new Label(done ? "<s>" + body.Replace("<", "<noparse><</noparse>") + "</s>" : body)
                {
                    enableRichText = done,
                    tooltip = "Click to edit"
                };
                label.style.flexGrow = 1; label.style.flexShrink = 1;
                label.style.whiteSpace = WhiteSpace.Normal;
                label.style.fontSize = Px(13);
                label.style.color = done ? EHTheme.Dim : EHTheme.Text;
                label.style.paddingLeft = 0; label.style.marginLeft = 0; label.style.paddingTop = 1;
                int endOfLine = ls + line.Length;
                label.RegisterCallback<PointerDownEvent>(e =>
                {
                    if (e.button != 0) return;
                    e.StopPropagation();
                    int i = _fields.IndexOf(f);
                    if (i >= 0) Focus(i, endOfLine);
                });
                row.Add(label);
                rows.Add(row);
            }
        }

        // ------------------------------------------------------------------ keys

        void OnKey(TextField f, KeyDownEvent e)
        {
            int bi = _fields.IndexOf(f);
            if (bi < 0) return;
            var block = _doc.Blocks[bi];
            string t = f.value ?? "";
            int cur = f.cursorIndex, sel = f.selectIndex;
            _caretBlock = bi; _caretPos = Mathf.Min(cur, sel); // where Undo puts the caret back
            bool noSel = cur == sel;
            bool textLike = MemoDoc.TextLike(block.kind);

            // ---- "/" menu: the slash is typed normally; a number replaces it with the choice
            if (_menuOpen)
            {
                char ch = e.character;
                if (ch >= '0' && ch <= '9')
                {
                    e.StopImmediatePropagation();
                    CloseMenu();
                    var mb = _doc.Blocks[_menuBlock];
                    if (_slashPos >= 0 && _slashPos < mb.text.Length && mb.text[_slashPos] == '/')
                    {
                        mb.text = mb.text.Remove(_slashPos, 1);
                        _selA = _selB = _slashPos;
                    }
                    Apply(ch - '0');
                    return;
                }
                if (ch == '/') { e.StopImmediatePropagation(); CloseMenu(); return; } // "//" = one typed slash
                if (ch != '\0' || IsNavigationKey(e.keyCode)) CloseMenu();
                return;
            }
            if (e.actionKey && e.keyCode == KeyCode.V && TryPasteImage(bi, cur))
            {
                e.StopImmediatePropagation();
                return;
            }
            if (e.character == '/' && block.kind != MemoDoc.Kind.Code)
            {
                _menuBlock = bi;
                _selA = cur; _selB = sel;
                if (!noSel) { _slashPos = -1; _expectSlash = false; e.StopImmediatePropagation(); }
                else { _slashPos = cur; _expectSlash = true; }
                OpenMenu(f);
                return;
            }

            // ---- Tab / Shift+Tab: indent list items (or a plain tab of spaces)
            if (e.keyCode == KeyCode.Tab || e.character == '\t')
            {
                e.StopImmediatePropagation();
                if (e.keyCode != KeyCode.Tab) return; // the character half of the key
                if (textLike && MemoDoc.ListIndent(t, cur, sel, e.shiftKey ? -1 : 1, out var nt, out int na, out int nb))
                    SetText(f, bi, nt, na, nb);
                else if (!e.shiftKey)
                {
                    int a = Mathf.Min(cur, sel), b = Mathf.Max(cur, sel);
                    SetText(f, bi, t.Remove(a, b - a).Insert(a, MemoDoc.Indent), a + MemoDoc.Indent.Length, a + MemoDoc.Indent.Length);
                }
                return;
            }

            // ---- Enter (Unity may send the key and the '\n' character separately)
            bool enter = e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter;
            if (!enter && (e.character == '\n' || e.character == '\r'))
            {
                if (_swallowNewline) { _swallowNewline = false; e.StopImmediatePropagation(); }
                return;
            }
            if (enter)
            {
                _swallowNewline = false;
                if (HandleEnter(f, bi, block, t, cur, sel, e.actionKey, e.shiftKey))
                {
                    _swallowNewline = e.character != '\n' && e.character != '\r';
                    e.StopImmediatePropagation();
                }
                return;
            }

            // ---- Space after a shortcut at the start of a line
            if (e.character == ' ' && noSel && textLike)
            {
                int ls = MemoDoc.LineStartAt(t, cur);
                string before = t.Substring(ls, cur - ls);
                MemoDoc.Kind kind = MemoDoc.Kind.Text;
                if (block.kind == MemoDoc.Kind.Text)
                {
                    switch (before)
                    {
                        case "#": kind = MemoDoc.Kind.Title; break;
                        case "##": kind = MemoDoc.Kind.Subtitle; break;
                        case ">": case "\"": kind = MemoDoc.Kind.Quote; break;
                        case "!": kind = MemoDoc.Kind.Callout; break;
                        case "```": kind = MemoDoc.Kind.Code; break;
                        case "[]": case "[ ]": kind = MemoDoc.Kind.Checklist; break;
                    }
                }
                if (kind != MemoDoc.Kind.Text)
                {
                    e.StopImmediatePropagation();
                    block.text = t.Remove(ls, before.Length);
                    int p = ls;
                    _doc.MakeBlock(ref bi, ref p, kind);
                    Restructure(bi, p);
                    return;
                }
                if (MemoDoc.ListShortcut(t, cur, out var nt, out int nc))
                {
                    e.StopImmediatePropagation();
                    SetText(f, bi, nt, nc, nc);
                }
                return;
            }

            // ---- Backspace
            if (e.keyCode == KeyCode.Backspace && noSel)
            {
                if (block.kind == MemoDoc.Kind.Checklist)
                {
                    int ls = MemoDoc.LineStartAt(t, cur);
                    bool afterBox = MemoDoc.BoxAt(t, cur) != '\0' && cur == ls + 2;
                    if (afterBox || cur == ls)
                    {
                        e.StopImmediatePropagation();
                        if (ls > 0)
                        {
                            int cut = afterBox ? 3 : 1; // join with the item above (its box stays)
                            SetText(f, bi, t.Remove(ls - 1, cut), ls - 1, ls - 1);
                        }
                        else
                        {
                            int p = cur;
                            _doc.ChecklistLineToText(ref bi, ref p); // first item → plain text line
                            Restructure(bi, p);
                        }
                    }
                    return;
                }
                if (textLike && MemoDoc.ListBackspace(t, cur, out var nt, out int nc))
                {
                    e.StopImmediatePropagation();
                    SetText(f, bi, nt, nc, nc);
                    return;
                }
                if (cur == 0)
                {
                    e.StopImmediatePropagation();
                    if (block.kind == MemoDoc.Kind.Text && bi > 0 && _doc.Blocks[bi - 1].kind == MemoDoc.Kind.Image && bi - 1 < _blocks.childCount)
                    {
                        _blocks[bi - 1].Focus(); // picture selected; Delete / × asks before removing it
                        return;
                    }
                    int p = 0;
                    _doc.BackspaceAtStart(ref bi, ref p);
                    Restructure(bi, p);
                }
                return;
            }

            // ---- ↑ ↓ between blocks
            if ((e.keyCode == KeyCode.UpArrow || e.keyCode == KeyCode.DownArrow) && !e.shiftKey)
            {
                bool up = e.keyCode == KeyCode.UpArrow;
                if (up ? MemoDoc.OnFirstLine(t, cur) : MemoDoc.OnLastLine(t, cur))
                {
                    int next = bi + (up ? -1 : 1);
                    while (next >= 0 && next < _fields.Count && _fields[next] == null) next += up ? -1 : 1;
                    if (next >= 0 && next < _fields.Count)
                    {
                        e.StopImmediatePropagation();
                        Focus(next, up ? int.MaxValue : 0);
                    }
                    else if (!up && block.kind != MemoDoc.Kind.Text)
                    {
                        // leave a styled block at the bottom: start a text block below
                        e.StopImmediatePropagation();
                        _doc.Blocks.Add(new MemoDoc.Block(MemoDoc.Kind.Text));
                        Restructure(_doc.Blocks.Count - 1, 0);
                    }
                }
            }
        }

        bool HandleEnter(TextField f, int bi, MemoDoc.Block block, string t, int cur, int sel, bool ctrl, bool shift)
        {
            if (cur != sel || shift) return false;

            // Ctrl+Enter: tick / untick a checklist item
            if (ctrl)
            {
                if (block.kind != MemoDoc.Kind.Checklist) return false;
                string toggled = MemoDoc.ToggleDoneAt(t, cur);
                if (toggled != t) SetText(f, bi, toggled, cur, cur);
                return true;
            }

            switch (block.kind)
            {
                case MemoDoc.Kind.Title:
                case MemoDoc.Kind.Subtitle:
                {
                    int p = cur;
                    _doc.SplitAfter(ref bi, ref p);
                    Restructure(bi, p);
                    return true;
                }
                case MemoDoc.Kind.Checklist:
                {
                    int ls = MemoDoc.LineStartAt(t, cur), le = MemoDoc.LineEndAt(t, cur);
                    bool emptyItem = le == ls || (le - ls <= 2 && MemoDoc.BoxAt(t, cur) != '\0');
                    if (emptyItem)
                    {
                        int p = cur;
                        _doc.ChecklistLineToText(ref bi, ref p); // Enter on an empty item ends the list
                        Restructure(bi, p);
                        return true;
                    }
                    string ins = "\n" + MemoDoc.BoxGlyph + " ";
                    SetText(f, bi, t.Insert(cur, ins), cur + ins.Length, cur + ins.Length);
                    return true;
                }
                case MemoDoc.Kind.Code:
                    return ExitOnEmptyLastLine(bi, t, cur);
                default:
                {
                    // lists first (Text, Quote, Callout)
                    if (MemoDoc.ListEnter(t, cur, out var nt, out int nc))
                    {
                        SetText(f, bi, nt, nc, nc);
                        return true;
                    }
                    if (block.kind != MemoDoc.Kind.Text) return ExitOnEmptyLastLine(bi, t, cur);

                    int ls = MemoDoc.LineStartAt(t, cur), le = MemoDoc.LineEndAt(t, cur);
                    string line = t.Substring(ls, le - ls);
                    if (line == "---")
                    {
                        block.text = t.Remove(ls, 3);
                        int p = ls;
                        _doc.InsertDivider(ref bi, ref p);
                        Restructure(bi, p);
                        return true;
                    }
                    if (line == "```")
                    {
                        block.text = t.Remove(ls, 3);
                        int p = ls;
                        _doc.MakeBlock(ref bi, ref p, MemoDoc.Kind.Code);
                        Restructure(bi, p);
                        return true;
                    }
                    return false; // ordinary new line
                }
            }
        }

        /// <summary>Quote / Code / Callout: Enter on an empty last line leaves the block.</summary>
        bool ExitOnEmptyLastLine(int bi, string t, int cur)
        {
            int ls = MemoDoc.LineStartAt(t, cur);
            if (!(ls == cur && cur == t.Length && ls > 0)) return false;
            int p = cur;
            _doc.ExitBlock(ref bi, ref p);
            Restructure(bi, p);
            return true;
        }

        static bool IsNavigationKey(KeyCode k) =>
            k == KeyCode.Escape || k == KeyCode.Tab || k == KeyCode.Return || k == KeyCode.KeypadEnter ||
            k == KeyCode.UpArrow || k == KeyCode.DownArrow || k == KeyCode.LeftArrow || k == KeyCode.RightArrow ||
            k == KeyCode.Home || k == KeyCode.End || k == KeyCode.PageUp || k == KeyCode.PageDown ||
            k == KeyCode.Backspace || k == KeyCode.Delete;

        // ------------------------------------------------------------------ "/" menu

        void OpenMenu(TextField f)
        {
            var root = EHMemoDesk.MenuLayer;
            if (root == null || root.panel != panel)
            {
                root = this;
                while (root.parent != null && root.parent.parent != null) root = root.parent;
            }
            if (_menu == null) _menu = new SlashMenu(n => { CloseMenu(); Apply(n); });
            if (_menu.parent != root) root.Add(_menu);
            _menu.Show(States(), root.WorldToLocal(f.worldBound));
            _menuOpen = true;
        }

        /// <summary>Short message over the Memo Desk.</summary>
        void Notice(string message)
        {
            var w = EditorWindow.GetWindow<EHMemoDesk>(false, null, false);
            w?.ShowNotification(new GUIContent(message), 1.4);
        }

        void CloseMenu()
        {
            _menuOpen = false;
            _menu?.RemoveFromHierarchy();
        }

        /// <summary>Ticks in the menu: the block's kind.</summary>
        bool[] States()
        {
            var b = _doc.Blocks[_menuBlock];
            return new[]
            {
                false,
                b.kind == MemoDoc.Kind.Title,
                b.kind == MemoDoc.Kind.Subtitle,
                b.kind == MemoDoc.Kind.Quote,
                b.kind == MemoDoc.Kind.Callout,
                b.kind == MemoDoc.Kind.Code,
                b.kind == MemoDoc.Kind.Checklist,
                false,
                false,
                false,
            };
        }

        /// <summary>
        /// 1 title, 2 subtitle, 3 quote, 4 callout, 5 code, 6 checklist (same again = back to text),
        /// 7 picture from the clipboard, 8 picture file, 9 divider, 0 remove checked items.
        /// </summary>
        void Apply(int n)
        {
            int bi = _menuBlock;
            int a = Mathf.Min(_selA, _selB), b = Mathf.Max(_selA, _selB);
            switch (n)
            {
                case 1: case 2: case 3: case 4: case 5: case 6:
                {
                    var kind = n == 1 ? MemoDoc.Kind.Title : n == 2 ? MemoDoc.Kind.Subtitle : n == 3 ? MemoDoc.Kind.Quote
                        : n == 4 ? MemoDoc.Kind.Callout : n == 5 ? MemoDoc.Kind.Code : MemoDoc.Kind.Checklist;
                    int p = _selA;
                    _doc.MakeBlock(ref bi, ref p, kind, _selB);
                    Restructure(bi, p);
                    break;
                }
                case 7:
                {
                    // picture from the clipboard (screenshot / Snipping Tool / Cmd+Shift+Ctrl+4)
                    var tex = EHMemoImages.ReadClipboard();
                    if (tex == null) { Restructure(bi, a); Notice("No picture on the clipboard"); break; }
                    InsertImage(bi, a, EHMemoImages.Save(tex));
                    break;
                }
                case 8:
                {
                    // picture file
                    var file = EditorUtility.OpenFilePanel("Add a picture to the memo", "", "png,jpg,jpeg");
                    if (string.IsNullOrEmpty(file)) { Restructure(bi, a); break; }
                    InsertImage(bi, a, EHMemoImages.Import(file));
                    break;
                }
                case 9:
                {
                    int p = a;
                    _doc.InsertDivider(ref bi, ref p);
                    Restructure(bi, p);
                    break;
                }
                case 0:
                {
                    var blk = _doc.Blocks[bi];
                    if (blk.kind == MemoDoc.Kind.Checklist)
                    {
                        blk.text = MemoDoc.ClearDone(blk.text);
                        if (blk.text.Length == 0) blk.text = MemoDoc.BoxGlyph + " ";
                    }
                    Restructure(bi, Mathf.Min(a, blk.text.Length));
                    break;
                }
            }
        }
    }

    /// <summary>Small list that opens on "/": number + name, a tick on what's on, and the markdown shortcuts.</summary>
    class SlashMenu : VisualElement
    {
        static readonly int[] Order = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 0 };
        static readonly string[] Names = { "Remove checked items", "Title", "Subtitle", "Quote", "Callout box", "Code", "Checklist", "Paste picture", "Picture file…", "Divider" };
        static readonly string[] Keys = { "", "#", "##", ">", "!", "```", "[]", "Ctrl+V", "drag in", "---" };
        readonly Label[] _ticks = new Label[10];

        public SlashMenu(Action<int> pick)
        {
            pickingMode = PickingMode.Position;
            style.position = Position.Absolute;
            style.width = 220;
            style.paddingTop = 4; style.paddingBottom = 6; style.paddingLeft = 4; style.paddingRight = 4;
            style.backgroundColor = EHTheme.GroupBg;
            MemoUI.Radius(this, 7);
            MemoUI.Border(this, EHTheme.Divider, 1f);

            foreach (int n in Order)
            {
                if (n == 7 || n == 9)
                {
                    var sep = new VisualElement { pickingMode = PickingMode.Ignore };
                    sep.style.height = 1; sep.style.marginTop = 3; sep.style.marginBottom = 3;
                    sep.style.backgroundColor = EHTheme.Divider;
                    Add(sep);
                }
                int num = n;
                var row = new VisualElement { focusable = false };
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.height = 21;
                row.style.paddingLeft = 6; row.style.paddingRight = 6;
                MemoUI.Radius(row, 5);
                var numLabel = new Label(num.ToString()) { pickingMode = PickingMode.Ignore };
                numLabel.style.width = 16;
                numLabel.style.color = EHTheme.Point;
                numLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                row.Add(numLabel);
                var name = new Label(Names[num]) { pickingMode = PickingMode.Ignore };
                name.style.flexGrow = 1;
                name.style.color = EHTheme.Text;
                row.Add(name);
                var key = new Label(Keys[num]) { pickingMode = PickingMode.Ignore };
                key.style.color = EHTheme.Dim;
                key.style.fontSize = 10;
                key.style.marginRight = 6;
                row.Add(key);
                var tick = new Label("✓") { pickingMode = PickingMode.Ignore };
                tick.style.color = EHTheme.Accent;
                tick.style.unityFontStyleAndWeight = FontStyle.Bold;
                tick.style.width = 12;
                row.Add(tick);
                _ticks[num] = tick;
                row.RegisterCallback<PointerEnterEvent>(_ => row.style.backgroundColor = EHTheme.CardHover);
                row.RegisterCallback<PointerLeaveEvent>(_ => row.style.backgroundColor = StyleKeyword.Null);
                row.RegisterCallback<PointerDownEvent>(e =>
                {
                    if (e.button != 0) return;
                    e.StopPropagation();
                    pick(num);
                }, TrickleDown.TrickleDown);
                Add(row);
            }

            var help = new Label("Shortcut: type the grey mark at the start of a line, then Space.\n" +
                                 "Lists: Enter = next item, Tab / Shift+Tab = indent, Enter on an empty item = out.\n" +
                                 "Checklist: Ctrl+Enter ticks while typing, or click the box.") { pickingMode = PickingMode.Ignore };
            help.style.fontSize = 10;
            help.style.color = EHTheme.Dim;
            help.style.whiteSpace = WhiteSpace.Normal;
            help.style.marginTop = 5; help.style.paddingLeft = 6; help.style.paddingRight = 6;
            Add(help);
        }

        public void Show(bool[] on, Rect fieldRect)
        {
            for (int i = 0; i < 10; i++) _ticks[i].style.visibility = on[i] ? Visibility.Visible : Visibility.Hidden;
            float h = 10 + 10 * 21 + 14 + 70;
            float top = fieldRect.yMax + 4f;
            var root = parent;
            if (root != null && top + h > root.layout.height) top = Mathf.Max(0f, fieldRect.y - h - 4f);
            style.left = fieldRect.x;
            style.top = top;
        }
    }

    // ====================================================================== page list

    /// <summary>
    /// A memo's pages: first line of each (body size, bold) + date, the current one marked; "+ New page" at the
    /// bottom. In Delete Mode every page gets a red × (asks first).
    /// </summary>
    class PagesMenu : VisualElement
    {
        public EHMemoCard Card { get; private set; }
        readonly VisualElement _rows;
        readonly ScrollView _scroll;
        VisualElement _host, _anchor;
        bool _deleteMode;
        int _confirm = -1;
        const float RowH = 24f, MaxRows = 9;
        EventCallback<PointerDownEvent> _outside;

        public PagesMenu()
        {
            pickingMode = PickingMode.Position;
            style.position = Position.Absolute;
            style.width = 240;
            style.paddingTop = 4; style.paddingBottom = 4; style.paddingLeft = 4; style.paddingRight = 4;
            style.backgroundColor = EHTheme.GroupBg;
            MemoUI.Radius(this, 7);
            MemoUI.Border(this, EHTheme.Divider, 1f);
            var title = new Label("PAGES") { pickingMode = PickingMode.Ignore };
            title.style.fontSize = 10;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = EHTheme.Dim;
            title.style.paddingLeft = 6; title.style.marginBottom = 2;
            Add(title);
            // long lists scroll (about 9 pages visible)
            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.style.maxHeight = RowH * MaxRows;
            Add(_scroll);
            _rows = new VisualElement();
            _scroll.Add(_rows);
            _outside = e =>
            {
                if (e.target is VisualElement ve && (ve == this || Contains(ve) || ve == _anchor || (_anchor != null && _anchor.Contains(ve)))) return;
                Close();
            };
        }

        public void Open(EHMemoCard card, VisualElement host, VisualElement anchorElement, Rect anchor, bool deleteMode)
        {
            Card = card;
            _anchor = anchorElement;
            _deleteMode = deleteMode;
            _confirm = -1;
            Build();
            if (parent != host) host.Add(this);
            _host = host;
            host.RegisterCallback(_outside, TrickleDown.TrickleDown);
            float w = 240f;
            float left = Mathf.Clamp(anchor.xMax - w, 4f, Mathf.Max(4f, host.layout.width - w - 4f));
            float h = 50f + Mathf.Min(card.Entry.pages.Count, MaxRows) * RowH;
            float top = anchor.yMax + 3f;
            if (top + h > host.layout.height) top = Mathf.Max(4f, anchor.y - h - 3f);
            style.left = left;
            style.top = top;
        }

        public void Close()
        {
            _host?.UnregisterCallback(_outside, TrickleDown.TrickleDown);
            _host = null;
            _anchor = null;
            Card = null;
            RemoveFromHierarchy();
        }

        void Build()
        {
            bool deleteMode = _deleteMode;
            _rows.Clear();
            var entry = Card.Entry;
            _ = entry.Page; // older memos: make sure there is a page list
            for (int i = 0; i < entry.pages.Count; i++)
            {
                int index = i;
                var page = entry.pages[i];
                bool on = i == entry.current;
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.height = 24;
                row.style.paddingLeft = 6; row.style.paddingRight = 4;
                MemoUI.Radius(row, 5);
                if (on) row.style.backgroundColor = EHDraw.WithAlpha(EHTheme.Accent, 0.14f);

                var mark = new Label(on ? "▸" : "") { pickingMode = PickingMode.Ignore };
                mark.style.width = 12;
                mark.style.color = EHTheme.Point;
                row.Add(mark);

                string first = MemoDoc.FirstLine(page.text);
                if (first.Length == 0) first = MemoDoc.FirstLine(page.text2);
                var name = new Label(first.Length > 0 ? first : "Untitled") { pickingMode = PickingMode.Ignore };
                name.style.flexGrow = 1; name.style.flexShrink = 1;
                name.style.overflow = Overflow.Hidden;
                name.style.textOverflow = TextOverflow.Ellipsis;
                name.style.whiteSpace = WhiteSpace.NoWrap;
                name.style.unityFontStyleAndWeight = FontStyle.Bold;
                name.style.color = first.Length > 0 ? EHTheme.Text : EHTheme.Dim;
                row.Add(name);

                var date = new Label(page.modified > 0 ? new DateTime(page.modified).ToString("MM/dd HH:mm") : "")
                    { pickingMode = PickingMode.Ignore };
                date.style.fontSize = 10;
                date.style.color = EHTheme.Accent; // light beige: readable on the dark list
                date.style.marginLeft = 6;
                date.style.flexShrink = 0;
                row.Add(date);

                if (deleteMode && entry.pages.Count > 1 && _confirm == index)
                {
                    // asking right on the row; the list stays open
                    row.Clear();
                    row.style.backgroundColor = new Color(0.55f, 0.18f, 0.18f, 0.35f);
                    var q = MemoUI.ConfirmLabel("Delete \"" + (first.Length > 0 ? first : "Untitled") + "\"?");
                    q.style.flexGrow = 1;
                    row.Add(q);
                    var no = MemoUI.SmallButton("Cancel", "Keep it", () => { _confirm = -1; Build(); });
                    no.style.height = 18; no.style.marginLeft = 4;
                    no.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
                    row.Add(no);
                    var yes = MemoUI.DangerButton("Delete", "Delete permanently", () =>
                    {
                        _confirm = -1;
                        Card.DeletePage(index);
                        Build();
                    });
                    yes.style.height = 18; yes.style.marginLeft = 4;
                    yes.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
                    row.Add(yes);
                    _rows.Add(row);
                    continue;
                }
                if (deleteMode && entry.pages.Count > 1)
                {
                    var del = MemoUI.DangerButton("×", "Delete this page", () => { _confirm = index; Build(); });
                    del.style.width = 22; del.style.height = 18;
                    del.style.paddingLeft = 0; del.style.paddingRight = 0;
                    del.style.marginLeft = 6;
                    del.RegisterCallback<PointerDownEvent>(e => e.StopPropagation()); // don't switch to the page
                    row.Add(del);
                }

                row.RegisterCallback<PointerEnterEvent>(_ => { if (!on) row.style.backgroundColor = EHTheme.CardHover; });
                row.RegisterCallback<PointerLeaveEvent>(_ => { if (!on) row.style.backgroundColor = StyleKeyword.Null; });
                row.RegisterCallback<PointerDownEvent>(e =>
                {
                    if (e.button != 0) return;
                    var card = Card;
                    Close();
                    card.SwitchPage(index, false);
                });
                _rows.Add(row);
            }

            var add = new Label("+  New page") { tooltip = "Start a new page; the others are kept" };
            add.style.height = 24;
            add.style.unityTextAlign = TextAnchor.MiddleLeft;
            add.style.paddingLeft = 18;
            add.style.marginTop = 2;
            add.style.color = EHTheme.Point;
            add.style.unityFontStyleAndWeight = FontStyle.Bold;
            MemoUI.Radius(add, 5);
            add.RegisterCallback<PointerEnterEvent>(_ => add.style.backgroundColor = EHTheme.CardHover);
            add.RegisterCallback<PointerLeaveEvent>(_ => add.style.backgroundColor = StyleKeyword.Null);
            add.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                var card = Card;
                Close();
                card.NewPage();
            });
            _rows.Add(add);
        }
    }

    // ====================================================================== small UI helpers (Alt+E look)

    static class MemoUI
    {
        public static readonly Color Danger = new Color(0.78f, 0.22f, 0.22f);
        public static readonly Color DangerHover = new Color(0.88f, 0.28f, 0.28f);

        /// <summary>Red with white text: every "remove" in the Memo Desk looks the same.</summary>
        public static Button DangerButton(string text, string tip, Action click)
        {
            var b = SmallButton(text, tip, click);
            b.userData = true; // SmallButton hover colors off
            b.style.backgroundColor = Danger;
            b.style.color = Color.white;
            b.style.unityFontStyleAndWeight = FontStyle.Bold;
            b.RegisterCallback<PointerEnterEvent>(_ => b.style.backgroundColor = DangerHover);
            b.RegisterCallback<PointerLeaveEvent>(_ => b.style.backgroundColor = Danger);
            return b;
        }

        public static void Radius(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = r; e.style.borderTopRightRadius = r;
            e.style.borderBottomLeftRadius = r; e.style.borderBottomRightRadius = r;
        }

        public static void Border(VisualElement e, Color c, float w)
        {
            e.style.borderTopWidth = w; e.style.borderBottomWidth = w; e.style.borderLeftWidth = w; e.style.borderRightWidth = w;
            e.style.borderTopColor = c; e.style.borderBottomColor = c; e.style.borderLeftColor = c; e.style.borderRightColor = c;
        }

        public static Label ConfirmLabel(string text)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.style.color = new Color(0.95f, 0.62f, 0.6f);
            l.style.fontSize = 11;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.flexShrink = 1;
            l.style.overflow = Overflow.Hidden;
            l.style.textOverflow = TextOverflow.Ellipsis;
            l.style.whiteSpace = WhiteSpace.NoWrap;
            return l;
        }

        public static Button SmallButton(string text, string tip, Action click)
        {
            var b = new Button(click) { text = text, tooltip = tip };
            b.style.height = 20;
            b.style.fontSize = 11;
            b.style.paddingLeft = 9; b.style.paddingRight = 9;
            b.style.marginLeft = 0; b.style.marginRight = 0;
            b.style.color = EHTheme.Text;
            b.style.backgroundColor = EHTheme.Card;
            Border(b, Color.clear, 0f);
            Radius(b, 6);
            b.RegisterCallback<PointerEnterEvent>(_ => { if (b.userData == null) b.style.backgroundColor = EHTheme.CardHover; });
            b.RegisterCallback<PointerLeaveEvent>(_ => { if (b.userData == null) b.style.backgroundColor = EHTheme.Card; });
            return b;
        }

        public static VisualElement Hint(string text)
        {
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            box.style.position = Position.Absolute;
            box.style.left = 16; box.style.right = 16; box.style.top = 60;
            box.style.paddingTop = 18; box.style.paddingBottom = 18;
            box.style.backgroundColor = EHTheme.GroupBg;
            Radius(box, 8);
            Border(box, EHTheme.Divider, 1f);
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.style.color = EHTheme.Dim;
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.whiteSpace = WhiteSpace.Normal;
            box.Add(l);
            return box;
        }
    }
}
#endif
