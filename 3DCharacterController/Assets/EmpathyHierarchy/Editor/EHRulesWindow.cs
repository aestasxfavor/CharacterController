using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace EmpathyHierarchy
{
    /// <summary>
    /// Rules: automatic looks. Pick the set in use, one at a time: Naming base, Component base
    /// or Custom (only your own rules). Each rule is shown as a Hierarchy row; click it to edit
    /// (the edit window opens right beside this one). An object's own style (Alt+E) always wins.
    /// </summary>
    public class EHRulesWindow : EditorWindow
    {
        const string ViewPref = "EmpathyHierarchy.RulesView";
        const float WideWidth = 1050f;    // from this width the rule list moves into a second column

        VisualElement _tabs, _preview, _look, _list, _footer;
        readonly List<IMGUIContainer> _looks = new List<IMGUIContainer>();
        bool _saveQueued;
        ScrollView _scroll, _page;
        bool _wide; // two columns: the rule list scrolls by itself; otherwise the whole page is one scroll

        /// <summary>The scroll the rule list moves with right now.</summary>
        ScrollView ListScroll => _wide ? _scroll : _page;
        Label _count;
        string _flashId;
        string _confirmId; // row (or "reset") asking "sure?"
        readonly Dictionary<StyleRule, int> _counts = new Dictionary<StyleRule, int>();
        bool _countsDirty = true, _rebuildQueued;
        StyleRule _editing; // rule open in the edit window (outlined)

        RuleGroup View
        {
            get => (RuleGroup)Mathf.Clamp(EditorPrefs.GetInt(ViewPref, (int)EHRules.instance.activeSet), 0, 2);
            set => EditorPrefs.SetInt(ViewPref, (int)value);
        }

        [MenuItem("Tools/Empathy Hierarchy/Rules", false, 23)]
        public static void OpenMenu() => Open(null);

        /// <summary>Opens in the middle of the editor (or focuses it); <paramref name="highlight"/> glows once.</summary>
        public static void Open(StyleRule highlight)
        {
            var open = Resources.FindObjectsOfTypeAll<EHRulesWindow>();
            EHRulesWindow w;
            if (open.Length > 0) w = open[0];
            else
            {
                w = CreateInstance<EHRulesWindow>();
                w.titleContent = new GUIContent("Rules");
                w.minSize = new Vector2(460, 360);
                var main = EditorGUIUtility.GetMainWindowPosition();
                var size = new Vector2(Mathf.Min(640f, main.width - 80f), Mathf.Min(980f, main.height - 60f));
                w.position = new Rect(main.center - size * 0.5f, size);
                w.Show();
            }
            if (highlight != null)
            {
                w._flashId = highlight.id;
                w.View = highlight.group;
            }
            w.Rebuild();
            w.Focus();
        }

        void OnEnable()
        {
            EHRules.Changed += OnSomethingChanged;
            EditorApplication.hierarchyChanged += OnSomethingChanged;
        }

        void OnDisable()
        {
            EHRules.Changed -= OnSomethingChanged;
            EditorApplication.hierarchyChanged -= OnSomethingChanged;
        }

        void OnSomethingChanged()
        {
            _countsDirty = true;
            if (_rebuildQueued) return;
            _rebuildQueued = true;
            EditorApplication.delayCall += () => { _rebuildQueued = false; if (this != null) Rebuild(); };
        }

        // ------------------------------------------------------------------ layout

        void CreateGUI()
        {
            rootVisualElement.style.backgroundColor = EHTheme.Bg;

            // One column: the whole page is one scroll (the rule list at full height).
            // Wide windows (a tab docked along the bottom) split into two columns that scroll on their own:
            // settings on the left, the rule list on the right, each as tall as the window.
            var page = _page = new ScrollView(ScrollViewMode.Vertical);
            page.style.flexGrow = 1;
            rootVisualElement.Add(page);
            // a pending "Delete?" goes away when anything else is clicked
            rootVisualElement.RegisterCallback<PointerDownEvent>(e =>
            {
                if (_confirmId == null) return;
                var t = e.target as VisualElement;
                if (_confirmBox != null && t != null && _confirmBox.Contains(t)) return;
                for (var p = t; p != null; p = p.parent) if (p.name == AskButton) return; // another row's ×
                rootVisualElement.schedule.Execute(CancelConfirm);
            }, TrickleDown.TrickleDown);
            var body = page.contentContainer;
            body.style.paddingLeft = 16; body.style.paddingRight = 16; body.style.paddingTop = 14; body.style.paddingBottom = 12;

            var leftScroll = new ScrollView(ScrollViewMode.Vertical); // scrolls only in two columns
            leftScroll.style.flexShrink = 0;
            body.Add(leftScroll);
            var root = new VisualElement(); // settings: header, tabs, preview, look
            root.style.flexShrink = 0;
            leftScroll.Add(root);
            var listSide = new VisualElement(); // the rule list and its buttons
            listSide.style.minHeight = 0;
            body.Add(listSide);

            bool? wide = null;
            Action layout = () =>
            {
                var vp = page.contentViewport.layout;
                if (float.IsNaN(vp.width) || vp.width <= 0f) return;
                bool w = vp.width >= WideWidth;
                if (wide != w)
                {
                    wide = w;
                    _wide = w;
                    var on = w ? ScrollerVisibility.Auto : ScrollerVisibility.Hidden;
                    page.verticalScrollerVisibility = w ? ScrollerVisibility.Hidden : ScrollerVisibility.Auto;
                    body.style.flexDirection = w ? FlexDirection.Row : FlexDirection.Column;

                    // left: half the width (760 at most), its own scroll
                    leftScroll.verticalScrollerVisibility = on;
                    leftScroll.style.width = w ? new StyleLength(Length.Percent(50)) : new StyleLength(StyleKeyword.Auto);
                    leftScroll.style.maxWidth = w ? new StyleLength(760f) : new StyleLength(StyleKeyword.None);
                    if (!w) leftScroll.scrollOffset = Vector2.zero;
                    root.style.paddingRight = w ? 8f : 0f; // room for its scrollbar

                    // right: the rule list fills the height and scrolls; the buttons stay at the bottom
                    listSide.style.flexGrow = w ? 1f : 0f;
                    listSide.style.flexBasis = w ? new StyleLength(0f) : new StyleLength(StyleKeyword.Auto);
                    listSide.style.marginLeft = w ? 16f : 0f;
                    _scroll.style.flexGrow = w ? 1f : 0f;
                    _scroll.style.flexShrink = w ? 1f : 0f;
                    _scroll.verticalScrollerVisibility = on;
                    if (!w) _scroll.scrollOffset = Vector2.zero;
                }
                if (w)
                {
                    // exactly the window: nothing scrolls as a whole
                    body.style.minHeight = StyleKeyword.Auto;
                    if (Mathf.Abs(body.layout.height - vp.height) > 0.5f) body.style.height = vp.height;
                }
                else
                {
                    // as tall as the content, at least the window
                    body.style.height = StyleKeyword.Auto;
                    body.style.minHeight = vp.height;
                }
            };
            page.RegisterCallback<GeometryChangedEvent>(_ => layout());

            var head = new VisualElement();
            head.style.flexDirection = FlexDirection.Row;
            head.style.alignItems = Align.Center;
            var title = new Label("RULES");
            title.style.color = EHTheme.Point;
            title.style.fontSize = 13;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            head.Add(title);
            _count = new Label();
            _count.style.width = 90;
            _count.style.color = EHTheme.Dim;
            _count.style.fontSize = 11;
            _count.style.marginLeft = 8;
            head.Add(_count);
            // presets: the whole look to / from a file (other projects, sharing)
            var headGap = new VisualElement();
            headGap.style.flexGrow = 1;
            head.Add(headGap);
            var import = MemoUI.SmallButton("Import", "Import a preset file (rules, theme, tone, settings)", EHPreset.OpenImport);
            import.style.marginRight = 4;
            head.Add(import);
            head.Add(MemoUI.SmallButton("Export", "Save rules, theme, tone and settings to a preset file", EHPreset.OpenExport));
            head.style.flexShrink = 0;
            head.style.marginBottom = 8;
            head.tooltip = "Objects get a look automatically. An object's own style (Alt+E) always wins.";
            root.Add(head);

            // rule type: big and clear
            _tabs = new VisualElement();
            _tabs.style.flexDirection = FlexDirection.Row;
            _tabs.style.backgroundColor = EHTheme.GroupBg;
            _tabs.style.paddingLeft = 4; _tabs.style.paddingRight = 4; _tabs.style.paddingTop = 4; _tabs.style.paddingBottom = 4;
            _tabs.style.flexShrink = 0;
            _tabs.style.height = 34;
            MemoUI.Radius(_tabs, 10);
            MemoUI.Border(_tabs, EHTheme.Divider, 1f);
            root.Add(_tabs);


            // preview (what happens inside a matched parent) + the three choices beside it
            _preview = new VisualElement();
            _preview.style.flexDirection = FlexDirection.Row;
            _preview.style.flexShrink = 0;
            _preview.style.marginTop = 8;
            _preview.style.marginBottom = 8;
            root.Add(_preview);

            // theme, fill, tone in one box
            _look = new VisualElement();
            _look.style.flexShrink = 0;
            root.Add(_look);

            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.style.flexGrow = 1;
            _scroll.style.flexShrink = 1;
            _scroll.style.minHeight = 0;
            listSide.Add(_scroll);
            _list = new VisualElement();
            _scroll.Add(_list);

            _footer = new VisualElement();
            _footer.style.flexDirection = FlexDirection.Row;
            _footer.style.alignItems = Align.Center;
            _footer.style.marginTop = 8;
            _footer.style.flexShrink = 0;
            listSide.Add(_footer);

            Rebuild();
        }

        void Rebuild()
        {
            if (_list == null) return;
            _confirmBox = null; // set again by the row / footer that is asking
            var rules = EHRules.instance;
            if (_countsDirty)
            {
                EHRules.CountAll(_counts);
                _countsDirty = false;
            }

            int active = 0;
            foreach (var r in rules.Ordered()) if (r.enabled && r.HasCondition) active++;
            _count.text = "·  " + active + " active";

            var view = View;
            BuildTabs(view, rules);
            _looks.Clear();
            BuildPreview(view);
            BuildLook(view);

            _list.Clear();
            _editing = EHStyleWindow.EditingRule;
            var list = rules.ListOf(view);
            bool applied = view == rules.activeSet;
            _list.style.opacity = applied ? 1f : 0.5f;
            if (list.Count == 0)
            {
                var hint = new Label(view == RuleGroup.Custom
                    ? "No custom rules yet.\nUse “Make Rule” in Edit Style (Alt+E), or + Add rule below."
                    : "This set is empty. Reset brings the built-in rules back.");
                hint.style.color = EHTheme.Dim;
                hint.style.fontSize = 11;
                hint.style.whiteSpace = WhiteSpace.Normal;
                hint.style.unityTextAlign = TextAnchor.MiddleCenter;
                hint.style.marginTop = 30;
                _list.Add(hint);
            }
            for (int i = 0; i < list.Count; i++) _list.Add(RuleRow(list, i));

            BuildFooter(view);
        }

        // ------------------------------------------------------------------ tone (whole project)

        static readonly string[] ToneNames = { "SATURATION", "BRIGHTNESS", "OPACITY" };

        /// <summary>Tone rows (whole project) at the bottom of the look box.</summary>
        void AddTone(VisualElement box)
        {
            var head = LookRow("TONE");
            head.style.marginTop = 6;
            head.tooltip = "Saturation, brightness and opacity of every color in the project";
            var gap = new VisualElement();
            gap.style.flexGrow = 1;
            head.Add(gap);
            box.Add(head);

            var sliders = new List<Slider>();
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                var row = LookRow(ToneNames[i]);
                row.style.height = 20;
                var slider = new Slider(0f, 1f) { value = GetTone(k) };
                slider.style.flexGrow = 1;
                slider.style.marginLeft = 0; slider.style.marginRight = 8;
                var pct = new Label(Mathf.RoundToInt(GetTone(k) * 100f) + "%");
                pct.style.color = EHTheme.Text;
                pct.style.fontSize = 11;
                pct.style.width = 40;
                pct.style.unityTextAlign = TextAnchor.MiddleRight;
                slider.RegisterValueChangedCallback(e =>
                {
                    float v = Mathf.Round(e.newValue * 100f) / 100f;
                    pct.text = Mathf.RoundToInt(v * 100f) + "%";
                    SetTone(k, v);
                });
                sliders.Add(slider);
                row.Add(slider);
                row.Add(pct);
                box.Add(row);
            }

            head.Add(MemoUI.SmallButton("Reset", "Saturation, brightness and opacity back to 100%", () =>
            {
                foreach (var sl in sliders) sl.value = 1f;
            }));
        }

        static float GetTone(int k)
        {
            var s = EHSettings.instance;
            return k == 0 ? s.globalSaturation : k == 1 ? s.globalBrightness : s.globalOpacity;
        }

        void SetTone(int k, float v)
        {
            var s = EHSettings.instance;
            if (k == 0) s.globalSaturation = v;
            else if (k == 1) s.globalBrightness = v;
            else s.globalOpacity = v;
            EHStore.NotifyChanged();
            EditorApplication.RepaintHierarchyWindow();
            foreach (var look in _looks) look.MarkDirtyRepaint();
            // save once the slider settles
            if (_saveQueued) return;
            _saveQueued = true;
            rootVisualElement.schedule.Execute(() => { _saveQueued = false; EHSettings.instance.SaveSettings(); }).ExecuteLater(400);
        }

        void BuildTabs(RuleGroup view, EHRules rules)
        {
            _tabs.Clear();
            _tabs.Add(Tab("Naming Rules Base",
                "A ready-made set filled in with names (Manager, Light, Level…).\nEdit any rule to add components too.", RuleGroup.Naming, view, rules));
            _tabs.Add(Tab("Component Rules Base",
                "A ready-made set filled in with components (Camera, Light, Canvas…).\nEdit any rule to add names too.", RuleGroup.Component, view, rules));
            _tabs.Add(Tab("Custom Rules",
                "Only your own rules, made from scratch.", RuleGroup.Custom, view, rules));
        }

        VisualElement Tab(string text, string tip, RuleGroup g, RuleGroup view, EHRules rules)
        {
            bool on = view == g;
            // one set at a time, so the highlighted tab is the set in use
            var t = new VisualElement { tooltip = tip + "\nOne set is in use at a time: the highlighted one. Top rule wins." };
            t.style.flexGrow = 1;
            t.style.flexBasis = 0;
            t.style.height = 26;
            t.style.marginLeft = 2; t.style.marginRight = 2;
            t.style.alignItems = Align.Center;
            t.style.justifyContent = Justify.Center;
            MemoUI.Radius(t, 8);
            t.style.backgroundColor = on ? EHTheme.Accent : Color.clear;

            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.style.fontSize = 12;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.color = on ? EHTheme.AccentText : EHTheme.Text;
            l.style.paddingTop = 0; l.style.paddingBottom = 0; l.style.marginTop = 0; l.style.marginBottom = 0;
            t.Add(l);
            t.RegisterCallback<PointerEnterEvent>(_ => { if (!on) t.style.backgroundColor = EHTheme.CardHover; });
            t.RegisterCallback<PointerLeaveEvent>(_ => { if (!on) t.style.backgroundColor = Color.clear; });
            t.RegisterCallback<PointerUpEvent>(e =>
            {
                if (e.button != 0) return;
                _confirmId = null;
                View = g;
                // one set at a time: the tab you pick is the set in use
                if (rules.activeSet != g)
                {
                    rules.activeSet = g;
                    EHRules.MarkChanged();
                }
                Rebuild();
            });
            return t;
        }

        /// <summary>Theme (palette) and fill for the built-in sets: one click changes every built-in rule.</summary>
        void BuildLook(RuleGroup view)
        {
            _look.Clear();
            bool custom = view == RuleGroup.Custom;
            string which = custom ? "custom rule" : "built-in rule";
            _look.style.backgroundColor = EHTheme.GroupBg;
            _look.style.paddingLeft = 12; _look.style.paddingRight = 12; _look.style.paddingTop = 8; _look.style.paddingBottom = 6;
            _look.style.marginBottom = 8;
            MemoUI.Radius(_look, 10);
            MemoUI.Border(_look, EHTheme.Divider, 1f);

            var names = EHStyleWindow.PaletteNameList;
            var themeRow = LookRow("THEME");
            for (int i = 0; i < names.Length; i++)
            {
                int p = i;
                themeRow.Add(BigChip(names[i], EHStyleWindow.Palette == p, "Recolor every " + which + " with the " + names[i] + " palette",
                    () => { EHStyleWindow.Palette = p; EHRules.RecolorFromPalette(custom); }));
            }
            _look.Add(themeRow);

            var fillRow = LookRow("FILL");
            fillRow.style.marginTop = 4;
            string[] fills = { "Soft", "Solid", "Outline" };
            for (int i = 0; i < fills.Length; i++)
            {
                var f = (FillMode)i;
                fillRow.Add(BigChip(fills[i], EHRules.instance.FillFor(view) == f, "Every " + which + " gets the " + fills[i] + " fill (new ones too)",
                    () => EHRules.SetFill(custom, f)));
            }
            _look.Add(fillRow);

            AddTone(_look);
        }

        // ------------------------------------------------------------------ preview + inside-a-match choice

        // rule shown in the preview, per tab (none = the neutral sample); set by clicking a rule
        readonly Dictionary<RuleGroup, string> _previewIds = new Dictionary<RuleGroup, string>();

        void BuildPreview(RuleGroup view)
        {
            _preview.Clear();
            var rules = EHRules.instance;
            var example = PreviewRule(view);
            var other = OtherLook(example);

            IMGUIContainer tree = null;
            tree = new IMGUIContainer(() =>
                EHStyleWindow.DrawRuleTree(new Rect(0, 0, tree.contentRect.width, tree.contentRect.height), example, other, rules.nested));
            tree.style.height = 98; // = four choice buttons (23 + 2 each) minus the last gap
            _looks.Add(tree);

            // caption above, like the choices on the right, so both columns line up
            var left = new VisualElement();
            left.style.flexGrow = 1;
            left.style.marginRight = 6;
            left.Add(Caption("PREVIEW"));
            left.Add(tree);
            _preview.Add(left);

            var col = new VisualElement();
            col.style.width = 176;
            col.Add(Caption("INSIDE A MATCHED PARENT"));
            col.Add(NestedChip("Top match only", NestedMode.TopMatchOnly, "Not matched when a parent already matches (a Managers group keeps its managers plain)"));
            col.Add(NestedChip("Match all", NestedMode.MatchAll, "Matched everywhere, with the full look"));
            col.Add(NestedChip("Match all · icon only", NestedMode.IconOnly, "Matched, but shows only its icon (no color)"));
            col.Add(NestedChip("Match all · own tint", NestedMode.Tinted,
                "Matched, with its own icon and its own color as a light child tint:\nit's not a top-level object, so it reads as part of the group"));
            _preview.Add(col);
        }

        StyleRule PreviewRule(RuleGroup view)
        {
            if (_previewIds.TryGetValue(view, out var id))
                foreach (var r in EHRules.instance.ListOf(view))
                    if (r.id == id) return r;
            // neutral sample: beige, the list's fill
            return new StyleRule
            {
                name = "Match object",
                style = new ObjectStyle { hasColor = true, color = EHTheme.Accent, fill = EHRules.instance.FillFor(view), tintChildren = true, bold = true }
            };
        }

        static Label Caption(string text)
        {
            var l = new Label(text);
            l.style.color = EHTheme.Dim;
            l.style.fontSize = 9;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.height = 14;
            l.style.marginBottom = 2;
            l.style.paddingLeft = 0;
            return l;
        }

        /// <summary>"Other match" is always a camera: the Cameras rule's look if there is one, else a blue sample.</summary>
        static ObjectStyle OtherLook(StyleRule example)
        {
            foreach (var r in EHRules.instance.component)
                if (r.name == "Cameras" && r.style.hasColor) return r.style;
            return new ObjectStyle { hasColor = true, color = EHStyleWindow.PaletteColor(0), fill = example.style.fill, tintChildren = true, bold = true };
        }

        Button NestedChip(string text, NestedMode mode, string tip)
        {
            var b = BigChip(text, EHRules.instance.nested == mode, tip, () => { EHRules.instance.nested = mode; EHRules.MarkChanged(); });
            b.style.flexGrow = 0;
            b.style.flexBasis = StyleKeyword.Auto;
            b.style.height = 23;
            b.style.fontSize = 11;
            b.style.marginRight = 0;
            b.style.marginBottom = 2;
            return b;
        }

        static VisualElement LookRow(string caption)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            var l = new Label(caption);
            l.style.color = EHTheme.Dim;
            l.style.fontSize = 10;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.width = 84;
            row.Add(l);
            return row;
        }

        /// <summary>Segment-style chip with readable text (theme / fill choices).</summary>
        static Button BigChip(string text, bool on, string tip, Action click)
        {
            var b = MemoUI.SmallButton(text, tip, click);
            b.style.height = 26;
            b.style.fontSize = 12;
            b.style.flexGrow = 1;
            b.style.flexBasis = 0;
            b.style.marginRight = 4;
            if (on)
            {
                b.userData = true;
                b.style.backgroundColor = EHTheme.Accent;
                b.style.color = EHTheme.AccentText;
                b.style.unityFontStyleAndWeight = FontStyle.Bold;
            }
            return b;
        }

        void BuildFooter(RuleGroup view)
        {
            _footer.Clear();
            var add = MemoUI.SmallButton("+ Add rule", "A new rule in this list", () => AddRule(view));
            add.userData = true;
            add.style.backgroundColor = EHTheme.Accent;
            add.style.color = EHTheme.AccentText;
            add.style.unityFontStyleAndWeight = FontStyle.Bold;
            add.style.height = 26;
            _footer.Add(add);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            _footer.Add(spacer);

            if (view == RuleGroup.Custom) return;
            if (_confirmId == "reset")
            {
                _footer.Add(MemoUI.ConfirmLabel(view == RuleGroup.Naming ? "Reset the Naming rules?  " : "Reset the Component rules?  "));
                var cancel = MemoUI.SmallButton("Cancel", null, CancelConfirm);
                cancel.style.marginRight = 4;
                _footer.Add(cancel);
                _footer.Add(MemoUI.DangerButton("Reset", null, () => { _confirmId = null; _confirmBox = null; EHRules.ResetGroup(view); }));
                _confirmBox = _footer;
            }
            else
            {
                // only this tab's set; the other set and the Custom rules stay as they are
                var reset = MemoUI.SmallButton(view == RuleGroup.Naming ? "Reset Naming Rules" : "Reset Component Rules",
                    "Put only this list back to its built-in rules (in the current theme). The other lists stay as they are.",
                    () => { _confirmId = "reset"; Rebuild(); });
                reset.name = AskButton;
                _footer.Add(reset);
            }
        }

        // ------------------------------------------------------------------ one rule = one Hierarchy-like row

        VisualElement RuleRow(List<StyleRule> list, int index)
        {
            var rule = list[index];
            var s = EHSettings.instance;
            bool editing = _editing == rule;

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.height = 30;
            row.style.marginBottom = 3;
            row.style.paddingLeft = 6; row.style.paddingRight = 6;
            Color rowBg = EHDraw.WindowBg;
            row.style.backgroundColor = rowBg;
            MemoUI.Radius(row, 6);
            MemoUI.Border(row, editing ? EHTheme.Point : Color.clear, editing ? 1.5f : 0f);
            if (!rule.enabled) row.style.opacity = 0.5f;

            // on / off
            var dot = new VisualElement { tooltip = rule.enabled ? "On (click to switch off)" : "Off (click to switch on)" };
            dot.style.width = 14; dot.style.height = 14; dot.style.marginRight = 8; dot.style.flexShrink = 0;
            MemoUI.Radius(dot, 4);
            MemoUI.Border(dot, EHTheme.Accent, 1.5f);
            dot.style.backgroundColor = rule.enabled ? EHTheme.Accent : Color.clear;
            if (rule.enabled)
            {
                var tick = new Label("✓") { pickingMode = PickingMode.Ignore };
                tick.style.color = EHTheme.AccentText;
                tick.style.fontSize = 10;
                tick.style.unityFontStyleAndWeight = FontStyle.Bold;
                tick.style.unityTextAlign = TextAnchor.MiddleCenter;
                tick.style.paddingLeft = 0; tick.style.paddingRight = 0; tick.style.paddingTop = 0; tick.style.paddingBottom = 0;
                tick.style.marginLeft = 0; tick.style.marginRight = 0; tick.style.marginTop = -1;
                dot.Add(tick);
            }
            dot.RegisterCallback<PointerDownEvent>(e =>
            {
                e.StopPropagation();
                if (e.button != 0) return;
                rule.enabled = !rule.enabled;
                EHRules.MarkChanged();
            });
            row.Add(dot);

            // the row exactly as the Hierarchy draws it (same code as the edit window's preview)
            var icon = RuleIcon(rule);
            IMGUIContainer look = null;
            look = new IMGUIContainer(() =>
            {
                var r = new Rect(0, 0, look.contentRect.width, look.contentRect.height);
                var e = Event.current;
                // [ ] Bold, right-aligned inside the bar
                var boldRect = new Rect(r.xMax - 52f, r.y, 48f, r.height);
                if (e.type == EventType.Repaint)
                {
                    EHStyleWindow.DrawRowLook(r, rule.style, rule.name, icon, rowBg, null);
                    DrawBoldToggle(boldRect, rule.style.bold, BoldTextColor(rule.style, rowBg));
                }
                else if (e.type == EventType.MouseDown && e.button == 0 && boldRect.Contains(e.mousePosition))
                {
                    e.Use();
                    rule.style.bold = !rule.style.bold;
                    EditorApplication.delayCall += EHRules.MarkChanged;
                }
                else if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
                {
                    _pressed = rule; // opened on release, unless it turned into a drag
                }
                else if (e.type == EventType.MouseUp && e.button == 0 && r.Contains(e.mousePosition) && _pressed == rule && _drag == null)
                {
                    _pressed = null;
                    e.Use();
                    EditorApplication.delayCall += () =>
                    {
                        _previewIds[rule.group] = rule.id; // the preview shows this rule from now on (this tab)
                        EHStyleWindow.OpenRule(rule, this);
                        Rebuild(); // outline the row being edited
                    };
                }
            }) { tooltip = rule.Summary + "\nClick to edit  ·  Drag to reorder" };
            look.style.height = 20;
            look.style.flexGrow = 1; look.style.flexShrink = 1;
            _looks.Add(look);
            row.Add(look);

            // which conditions the rule uses (like the T / L circles in the Hierarchy)
            row.Add(ConditionCircle("N", rule.names, "Name contains: ", rowBg));
            var c = ConditionCircle("C", rule.components, "Has component: ", rowBg);
            c.style.marginLeft = 2;
            row.Add(c);

            // matches in the open scenes; click selects them
            _counts.TryGetValue(rule, out int count);
            var countChip = MemoUI.SmallButton(count.ToString(), count > 0 ? "Matches in the open scenes (click to select them)" : "Nothing matches in the open scenes",
                () =>
                {
                    var found = new List<GameObject>();
                    EHRules.Collect(rule, found);
                    Selection.objects = found.ToArray();
                    if (found.Count > 0) EditorGUIUtility.PingObject(found[0]);
                });
            countChip.style.minWidth = 32;
            countChip.style.marginLeft = 6; countChip.style.marginRight = 4;
            countChip.style.color = count > 0 ? EHTheme.Point : EHTheme.Dim;
            row.Add(countChip);

            var up = MemoUI.SmallButton("▲", "Earlier wins first", () => Move(list, index, -1));
            up.SetEnabled(index > 0);
            up.style.paddingLeft = 5; up.style.paddingRight = 5; up.style.marginRight = 2;
            row.Add(up);
            var down = MemoUI.SmallButton("▼", "Later", () => Move(list, index, 1));
            down.SetEnabled(index < list.Count - 1);
            down.style.paddingLeft = 5; down.style.paddingRight = 5; down.style.marginRight = 4;
            row.Add(down);

            if (_confirmId == rule.id)
            {
                // Cancel / Delete; a click anywhere else (or leaving the window) cancels too
                var box = new VisualElement();
                box.style.flexDirection = FlexDirection.Row;
                var no = MemoUI.SmallButton("Cancel", "Keep this rule", CancelConfirm);
                no.style.marginRight = 3;
                box.Add(no);
                box.Add(MemoUI.DangerButton("Delete", "Delete this rule", () => { _confirmId = null; _confirmBox = null; list.Remove(rule); EHRules.MarkChanged(); }));
                _confirmBox = box;
                row.Add(box);
            }
            else
            {
                var x = MemoUI.SmallButton("×", "Delete this rule", () => { _confirmId = rule.id; Rebuild(); });
                x.name = AskButton; // switches the question to this row itself
                x.style.paddingLeft = 6; x.style.paddingRight = 6;
                row.Add(x);
            }

            if (_flashId != null && rule.id == _flashId)
            {
                _flashId = null;
                Flash(row, rowBg);
            }
            HookDrag(row, look, list, index, icon);
            return row;
        }

        static void DrawBoldToggle(Rect r, bool on, Color text)
        {
            var box = new Rect(r.x, r.center.y - 6f, 12f, 12f);
            EHDraw.Rounded(box, on ? text : EHDraw.WithAlpha(text, 0.15f), 3f);
            if (!on) EHDraw.RoundedBorder(box, EHDraw.WithAlpha(text, 0.7f), 3f, 1f);
            else EHDraw.Text(box, "✓", EHDraw.Mini(true), EHDraw.ContrastText(text), TextAnchor.MiddleCenter);
            EHDraw.Text(new Rect(box.xMax + 4f, r.y, r.width - 16f, r.height), "Bold", EHDraw.Mini(on), text, TextAnchor.MiddleLeft);
        }

        /// <summary>Text color the row's label uses (so the checkbox reads on any fill).</summary>
        static Color BoldTextColor(ObjectStyle st, Color rowBg)
        {
            if (!st.hasColor) return EHDraw.DefaultText;
            Color c = EHDraw.Opaque(st.Color1);
            Color fa = EHHierarchyGUI.FillColor(c, st.fill, rowBg);
            if (st.gradient != GradientMode.None)
                fa = st.gradient == GradientMode.TwoColor ? EHHierarchyGUI.FillColor(EHDraw.Opaque(st.Color2), st.fill, rowBg) : rowBg;
            return st.fill == FillMode.Solid && st.gradient == GradientMode.None ? EHDraw.ContrastText(fa) : EHDraw.TintedText(c, fa);
        }

        /// <summary>"N" / "C" circle: lit when the rule has that condition (tooltip lists it).</summary>
        static Label ConditionCircle(string letter, string list, string label, Color under)
        {
            bool on = !string.IsNullOrWhiteSpace(list);
            EHTagLayer.ChipColors(!on, EHTheme.Point, under, out var bg, out var fg);
            var l = new Label(letter) { tooltip = on ? label + list.Trim() : label + "—" };
            l.style.width = 16; l.style.height = 16;
            l.style.marginLeft = 6;
            l.style.flexShrink = 0;
            MemoUI.Radius(l, 8);
            l.style.fontSize = 9;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.paddingLeft = 0; l.style.paddingRight = 0; l.style.paddingTop = 0; l.style.paddingBottom = 0;
            l.style.backgroundColor = bg;
            l.style.color = fg;
            return l;
        }

        /// <summary>Icon the row shows: custom icon, else the first listed component's icon, else a GameObject.</summary>
        static Texture RuleIcon(StyleRule rule)
        {
            if (rule.style.iconMode == IconMode.Custom)
            {
                var t = EHIcons.Find(rule.style.icon);
                if (t != null) return t;
            }
            if (rule.style.iconMode == IconMode.Auto && !string.IsNullOrWhiteSpace(rule.components))
            {
                var first = rule.components.Split(',')[0].Trim().TrimEnd('*');
                var t = EHIcons.Find(first + " Icon");
                if (t != null) return t;
            }
            return EHIcons.Find("GameObject Icon");
        }

        VisualElement _confirmBox;
        const string AskButton = "eh-rule-ask-delete";

        void CancelConfirm()
        {
            if (_confirmId == null) return;
            _confirmId = null;
            _confirmBox = null;
            Rebuild();
        }

        void OnLostFocus() => CancelConfirm();

        // ------------------------------------------------------------------ drag to reorder
        // Like the bookmark chips: the row lifts into a ghost that follows the pointer, the others slide
        // out of the way, and the row's own slot (outlined) glides to where it will land.

        const float RowStep = 33f; // row height 30 + gap 3
        StyleRule _pressed;
        DragState _drag;

        class DragState
        {
            public List<StyleRule> list;
            public int from, to;
            public VisualElement row, ghost;
            public float grabY;
            public int pointerId;
        }

        static readonly StyleList<StylePropertyName> TranslateProp =
            new StyleList<StylePropertyName>(new List<StylePropertyName> { new StylePropertyName("translate") });
        static readonly StyleList<TimeValue> SlideTime =
            new StyleList<TimeValue>(new List<TimeValue> { new TimeValue(150f, TimeUnit.Millisecond) });
        static readonly StyleList<EasingFunction> SlideEase =
            new StyleList<EasingFunction>(new List<EasingFunction> { new EasingFunction(EasingMode.EaseOutCubic) });

        void HookDrag(VisualElement row, IMGUIContainer look, List<StyleRule> list, int index, Texture icon)
        {
            row.style.transitionProperty = TranslateProp;
            row.style.transitionDuration = SlideTime;
            row.style.transitionTimingFunction = SlideEase;

            Vector2 start = default;
            bool armed = false;
            row.RegisterCallback<PointerDownEvent>(e =>
            {
                // only from the bar or the row's empty space (buttons keep their clicks)
                armed = e.button == 0 && (e.target == look || e.target == row) && _drag == null;
                start = e.position;
            }, TrickleDown.TrickleDown);
            row.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (_drag != null && _drag.row == row) { DragMove(e.position); return; }
                if (!armed || (e.pressedButtons & 1) == 0) { armed = false; return; }
                if (Mathf.Abs(e.position.y - start.y) < 5f) return;
                armed = false;
                DragBegin(row, list, index, icon, start, e.pointerId);
                DragMove(e.position);
            }, TrickleDown.TrickleDown);
            row.RegisterCallback<PointerUpEvent>(e =>
            {
                armed = false;
                if (_drag == null || _drag.row != row) return;
                e.StopPropagation();
                DragEnd(true);
            }, TrickleDown.TrickleDown);
            row.RegisterCallback<PointerCaptureOutEvent>(_ => { if (_drag != null && _drag.row == row) DragEnd(true); });
        }

        void DragBegin(VisualElement row, List<StyleRule> list, int index, Texture icon, Vector2 start, int pointerId)
        {
            _pressed = null;
            var rule = list[index];
            var rb = row.worldBound;
            var ghost = new VisualElement { pickingMode = PickingMode.Ignore };
            ghost.style.position = Position.Absolute;
            ghost.style.width = rb.width;
            ghost.style.height = rb.height;
            ghost.style.paddingLeft = 28; ghost.style.paddingRight = 8;
            ghost.style.justifyContent = Justify.Center;
            ghost.style.backgroundColor = EHDraw.WindowBg;
            ghost.style.opacity = 0.92f;
            MemoUI.Radius(ghost, 6);
            MemoUI.Border(ghost, EHTheme.Point, 1.5f);
            IMGUIContainer bar = null;
            bar = new IMGUIContainer(() =>
            {
                if (Event.current.type != EventType.Repaint) return;
                EHStyleWindow.DrawRowLook(new Rect(0, 0, bar.contentRect.width, bar.contentRect.height),
                    rule.style, rule.name, icon, EHDraw.WindowBg, null);
            }) { pickingMode = PickingMode.Ignore };
            bar.style.height = 20;
            ghost.Add(bar);
            rootVisualElement.Add(ghost);

            // the row stays as the landing slot: faded and outlined
            row.style.opacity = 0.35f;
            MemoUI.Border(row, EHTheme.Point, 1f);
            row.CapturePointer(pointerId);

            _drag = new DragState
            {
                list = list, from = index, to = index, row = row, ghost = ghost,
                grabY = start.y - rb.y, pointerId = pointerId
            };
        }

        void DragMove(Vector2 pointer)
        {
            var d = _drag;
            if (d == null) return;
            var rb = d.row.worldBound;
            var local = rootVisualElement.WorldToLocal(new Vector2(rb.x, pointer.y - d.grabY));
            d.ghost.style.left = local.x;
            d.ghost.style.top = local.y;

            // keep scrolling while held near the list's top / bottom edge
            var sc = ListScroll;
            var vb = sc.contentViewport.worldBound;
            if (pointer.y < vb.y + 24f) sc.scrollOffset -= new Vector2(0f, 10f);
            else if (pointer.y > vb.yMax - 24f) sc.scrollOffset += new Vector2(0f, 10f);

            // where the ghost's middle is in the list = the slot it lands in
            float top = _list.WorldToLocal(new Vector2(0f, pointer.y - d.grabY)).y;
            int to = Mathf.Clamp(Mathf.RoundToInt(top / RowStep), 0, d.list.Count - 1);
            if (to == d.to) return;
            d.to = to;
            for (int i = 0; i < _list.childCount && i < d.list.Count; i++)
            {
                var row = _list[i];
                float y = 0f;
                if (i == d.from) y = (to - d.from) * RowStep;
                else if (d.from < to && i > d.from && i <= to) y = -RowStep;
                else if (to < d.from && i >= to && i < d.from) y = RowStep;
                row.style.translate = new Translate(0f, y);
            }
        }

        void DragEnd(bool apply)
        {
            var d = _drag;
            if (d == null) return;
            _drag = null;
            d.ghost.RemoveFromHierarchy();
            if (d.row.HasPointerCapture(d.pointerId)) d.row.ReleasePointer(d.pointerId);
            if (apply && d.to != d.from)
            {
                var r = d.list[d.from];
                d.list.RemoveAt(d.from);
                d.list.Insert(d.to, r);
                EHRules.MarkChanged(); // rebuilds the list in the new order
            }
            else Rebuild();
        }

        void Move(List<StyleRule> list, int index, int delta)
        {
            int to = index + delta;
            if (to < 0 || to >= list.Count) return;
            var r = list[index];
            list.RemoveAt(index);
            list.Insert(to, r);
            EHRules.MarkChanged();
        }

        void AddRule(RuleGroup group)
        {
            var rule = new StyleRule
            {
                id = Guid.NewGuid().ToString("N"),
                group = group,
                name = "New Rule",
                paletteSlot = 0,
                style = new ObjectStyle
                {
                    hasColor = true, color = EHStyleWindow.PaletteColor(0), fill = EHRules.instance.FillFor(group),
                    tintChildren = true, bold = true,
                    iconMode = group == RuleGroup.Component ? IconMode.Auto : IconMode.Default
                }
            };
            // end of the list (scrolled to and flashed); move it up with ▲ to give it priority
            EHRules.instance.ListOf(group).Add(rule);
            _flashId = rule.id;
            EHRules.MarkChanged();
            ScrollListToEnd();
            EHStyleWindow.OpenRule(rule, this);
        }

        /// <summary>Rule list scrolled all the way down once the new row has its size.</summary>
        void ScrollListToEnd()
        {
            if (_scroll == null) return;
            Action toEnd = () =>
            {
                var sc = ListScroll;
                sc.verticalScroller.value = sc.verticalScroller.highValue;
            };
            EventCallback<GeometryChangedEvent> onLayout = null;
            onLayout = _ =>
            {
                _list.UnregisterCallback(onLayout);
                _scroll.schedule.Execute(toEnd);
            };
            _list.RegisterCallback(onLayout);
            // the list may keep its size (no layout event): scroll on the next frames too
            _scroll.schedule.Execute(toEnd).ExecuteLater(50);
        }

        /// <summary>A just-made rule glows briefly and scrolls into view.</summary>
        void Flash(VisualElement row, Color baseColor)
        {
            int frame = 0;
            row.schedule.Execute(() =>
            {
                frame++;
                if (frame == 2) ListScroll?.ScrollTo(row); // once the row has been laid out
                float k = Mathf.Abs(Mathf.Sin(frame / 30f * Mathf.PI * 2f));
                row.style.backgroundColor = Color.Lerp(baseColor, EHTheme.Point, k * 0.45f);
                if (frame >= 30) row.style.backgroundColor = baseColor;
            }).Every(40).Until(() => frame >= 30);
        }
    }
}
