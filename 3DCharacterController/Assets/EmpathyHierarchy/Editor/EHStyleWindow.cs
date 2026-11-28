using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EmpathyHierarchy
{
    /// <summary>
    /// "Edit Style" popup. Changes preview live in the Hierarchy; Apply keeps them as one Undo step,
    /// Cancel (or Esc) reverts everything.
    /// </summary>
    public class EHStyleWindow : EditorWindow
    {
        const float W = 440f;
        const float H = 912f;   // preferred height; clamped to the screen
        const float MinH = 420f;
        const float Pad = 16f;

        // theme colors (shared with the other Empathy Hierarchy popups)
        static readonly Color Bg = EHTheme.Bg;
        static readonly Color Card = EHTheme.Card;
        static readonly Color CardHover = EHTheme.CardHover;
        static readonly Color Divider = EHTheme.Divider;
        static readonly Color TextCol = EHTheme.Text;
        static readonly Color Dim = EHTheme.Dim;
        static readonly Color Accent = EHTheme.Accent;
        static readonly Color AccentText = EHTheme.AccentText;
        static readonly Color GroupBg = EHTheme.GroupBg;
        static readonly Color Point = EHTheme.Point;

        // two object palettes; the choice is remembered per user
        static readonly Color[] ClassicPresets =
        {
            Hex("#3A8FD8"), Hex("#3DBA6B"), Hex("#E5B935"), Hex("#EB8232"),
            Hex("#E5484D"), Hex("#9B6BE8"), Hex("#8C939D")
        };
        static readonly Color[] CocoaPresets =
        {
            // warm, muted vintage tones that still follow the slots (blue, green, yellow, orange, red, purple, gray):
            // dusty slate, sage, mustard, terracotta, brick, plum, warm taupe
            Hex("#6F8FAF"), Hex("#7E9A62"), Hex("#D4A84B"), Hex("#C77A45"),
            Hex("#A8494A"), Hex("#8B5A8F"), Hex("#9A8E82")
        };
        static readonly Color[] PastelPresets =
        {
            Hex("#8FB8DE"), Hex("#9ED4B0"), Hex("#F2D58A"), Hex("#F4B183"),
            Hex("#EE9A9F"), Hex("#B9A3E8"), Hex("#B7BCC4")
        };
        static readonly Color[] DeepPresets =
        {
            Hex("#1F4E79"), Hex("#1E6B47"), Hex("#8A6D1E"), Hex("#8F4A1D"),
            Hex("#8B2C31"), Hex("#4F3A86"), Hex("#4A5058")
        };

        // palette themes; the choice is remembered per user
        static readonly string[] PaletteNames = { "Classic", "Cocoa", "Pastel", "Deep" };
        static readonly string[] PaletteTips = { "Bright palette", "Warm brown palette", "Soft light palette", "Dark rich palette" };
        static readonly Color[][] PaletteSets = { ClassicPresets, CocoaPresets, PastelPresets, DeepPresets };
        const string PalettePrefKey = "EmpathyHierarchy.Palette";
        static int _palette = -1;

        static int PaletteIndex
        {
            get
            {
                if (_palette < 0) _palette = Mathf.Clamp(EditorPrefs.GetInt(PalettePrefKey, 1), 0, PaletteSets.Length - 1);
                return _palette;
            }
            set
            {
                _palette = value;
                EditorPrefs.SetInt(PalettePrefKey, _palette);
            }
        }

        static Color[] Presets => PaletteSets[PaletteIndex];

        /// <summary>Color of a slot (0 blue … 6 gray) in the palette chosen in Edit Style; used by rules.</summary>
        internal static Color PaletteColor(int slot) => Presets[Mathf.Clamp(slot, 0, Presets.Length - 1)];
        internal static string[] PaletteNameList => PaletteNames;
        internal static int Palette { get => PaletteIndex; set => PaletteIndex = value; }

        static readonly string[] IconTabs = { "Default", "Auto", "All" };

        GameObject[] _targets;
        ObjectStyle _draft;
        // rule mode: editing a rule's look and conditions instead of objects
        StyleRule _rule, _ruleOriginal;
        // object mode: the rule that styles the first target (shown as a chip), and whether it has its own style
        StyleRule _targetRule;
        bool _targetHasOwn;
        bool RuleMode => _rule != null;
        string _name;
        bool _isSeparator;
        GameObject[] _allSeparators;   // every separator in the open scenes (for "All separators")
        const string AllSepPref = "EmpathyHierarchy.EditAllSeparators";
        static bool AllSeparators
        {
            get => EditorPrefs.GetBool(AllSepPref, true);
            set => EditorPrefs.SetBool(AllSepPref, value);
        }
        int _undoGroup;
        bool _done;
        int _iconTab;
        string _hex;
        string _hex2;
        string _iconSearch = "";
        Vector2 _iconScroll;

        GUIStyle _title, _sub, _section, _field, _hexField, _barTitle, _plus;
        GUIStyle _dimField, _valueRight, _gridMessage, _placeholder;
        string _multiLabel;

        // reused so tooltips / labels don't allocate every OnGUI event
        static readonly GUIContent TipContent = new GUIContent();
        static readonly string[] Percents = new string[101];
        static readonly System.Collections.Generic.Dictionary<Color, string> PresetHexCache =
            new System.Collections.Generic.Dictionary<Color, string>();

        static GUIContent Tip(string tooltip)
        {
            TipContent.text = string.Empty;
            TipContent.image = null;
            TipContent.tooltip = tooltip;
            return TipContent;
        }

        static string Percent(float v)
        {
            int i = Mathf.Clamp(Mathf.RoundToInt(v * 100f), 0, 100);
            return Percents[i] ?? (Percents[i] = i + "%");
        }

        // ------------------------------------------------------------------ open / close

        public static void Open(GameObject[] targets)
        {
            if (targets == null || targets.Length == 0)
            {
                Debug.Log("[Empathy Hierarchy] Select one or more GameObjects first.");
                return;
            }

            foreach (var existing in Resources.FindObjectsOfTypeAll<EHStyleWindow>()) existing.Close();

            var w = CreateInstance<EHStyleWindow>();
            w.Init(targets);
            Show(w);
        }

        /// <summary>Edits a rule with the same window: the look as usual, plus the rule's conditions.</summary>
        public static void OpenRule(StyleRule rule, EditorWindow beside = null)
        {
            if (rule == null) return;
            foreach (var existing in Resources.FindObjectsOfTypeAll<EHStyleWindow>()) existing.Close();
            var w = CreateInstance<EHStyleWindow>();
            w.InitRule(rule);
            Show(w, beside);
        }

        /// <summary>The rule being edited right now (the Rules window highlights it), or null.</summary>
        internal static StyleRule EditingRule
        {
            get
            {
                foreach (var w in Resources.FindObjectsOfTypeAll<EHStyleWindow>())
                    if (w._rule != null) return w._rule;
                return null;
            }
        }

        static void Show(EHStyleWindow w, EditorWindow beside = null)
        {
            w.titleContent = new GUIContent("Empathy Hierarchy");
            w.wantsMouseMove = true;

            var hierarchy = EHNav.FindHierarchy(); // before showing: afterwards the new window has focus
            var main = EditorGUIUtility.GetMainWindowPosition();
            var anchor = focusedWindow != null ? focusedWindow.position : main;
            float h = Mathf.Min(H, main.height - 60f);
            w.minSize = new Vector2(W, MinH);
            w.maxSize = new Vector2(W, 4000f);
            w.ShowUtility();
            float y = Mathf.Max(main.y + 10f, anchor.y + 40f);
            if (y + h > main.yMax - 10f) y = Mathf.Max(main.y + 10f, main.yMax - 10f - h);
            float x = anchor.center.x - W * 0.5f;
            if (beside != null)
            {
                // next to the window it was opened from (Rules), right if there is room, else left
                var br = beside.position;
                x = br.xMax + 2f;
                if (x + W > main.xMax) x = br.xMin - W - 2f;
                if (x < main.xMin) x = main.xMin + 10f;
                y = Mathf.Max(main.y + 10f, br.y);
                if (y + h > main.yMax - 10f) y = Mathf.Max(main.y + 10f, main.yMax - 10f - h);
            }
            else if (hierarchy != null)
            {
                // always just right of the Hierarchy (over whatever is docked there) so its rows stay visible;
                // to the left only when the Hierarchy sits at the right edge of the screen
                var hr = hierarchy.position;
                x = hr.xMax + 2f;
                if (x + W > main.xMax) x = hr.xMin - W - 2f;
                if (x < main.xMin) x = main.xMin + 10f;
                y = Mathf.Max(main.y + 10f, hr.y);
                if (y + h > main.yMax - 10f) y = Mathf.Max(main.y + 10f, main.yMax - 10f - h);
            }
            w.position = new Rect(Mathf.Max(0, x), y, W, h);
            w.Focus();
        }

        void Init(GameObject[] targets)
        {
            _targets = targets;
            var existing = EHStore.Get(targets[0]);
            _draft = existing != null ? existing.Clone() : new ObjectStyle();
            _draft.id = null;
            _name = targets.Length == 1 ? targets[0].name : null;
            _targetRule = EHRules.Match(targets[0]);
            _targetHasOwn = EHStore.GetManual(targets[0]) != null;

            var s = EHSettings.instance;
            _isSeparator = true;
            foreach (var t in targets)
                if (!EHHierarchyGUI.IsSeparator(t.name, s)) { _isSeparator = false; break; }

            if (_isSeparator) _allSeparators = FindAllSeparators(s);

            _iconTab = _draft.iconMode == IconMode.Default ? 0 : _draft.iconMode == IconMode.Auto ? 1 : 2;
            _hex = ToHex(_draft.color);
            _hex2 = ToHex(_draft.color2);

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Edit Hierarchy Style");
            _undoGroup = Undo.GetCurrentGroup();
        }

        void InitRule(StyleRule rule)
        {
            _rule = rule;
            _ruleOriginal = rule.Clone();
            var sample = new List<GameObject>(1);
            EHRules.Collect(rule, sample, 1);
            _targets = sample.ToArray(); // preview object (may be empty)
            _draft = rule.style.Clone();
            _draft.id = null;
            _name = null;
            _isSeparator = false;
            _iconTab = _draft.iconMode == IconMode.Default ? 0 : _draft.iconMode == IconMode.Auto ? 1 : 2;
            _hex = ToHex(_draft.color);
            _hex2 = ToHex(_draft.color2);
        }

        void Push()
        {
            if (RuleMode)
            {
                _rule.style.CopyVisualsFrom(_draft);
                _rule.paletteSlot = PaletteSlotOf(_draft.color); // keeps following palette changes if picked from it
                _rule.paletteSlot2 = PaletteSlotOf(_draft.color2);
                EHRules.MarkChangedLive();
                Repaint();
                return;
            }
            _targetHasOwn = true; // the object now has its own style on top of any rule
            var d = _draft;
            var targets = _isSeparator && AllSeparators && _allSeparators != null ? _allSeparators : _targets;
            EHStore.Modify(targets, st => st.CopyVisualsFrom(d), "Edit Hierarchy Style");
            Repaint();
        }

        static GameObject[] FindAllSeparators(EHSettings s)
        {
            var list = new List<GameObject>();
            var stack = new Stack<Transform>();
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects()) stack.Push(root.transform);
            }
            while (stack.Count > 0)
            {
                var t = stack.Pop();
                if (EHHierarchyGUI.IsSeparator(t.name, s)) list.Add(t.gameObject);
                for (int c = 0; c < t.childCount; c++) stack.Push(t.GetChild(c));
            }
            return list.ToArray();
        }

        /// <summary>Switching off "All separators" puts the other separators back as they were.</summary>
        void SetAllSeparators(bool on)
        {
            if (on == AllSeparators) return;
            AllSeparators = on;
            if (!on)
            {
                Undo.RevertAllDownToGroup(_undoGroup);
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName("Edit Hierarchy Style");
                _undoGroup = Undo.GetCurrentGroup();
                EHStore.NotifyReverted();
            }
            Push();
        }

        static int PaletteSlotOf(Color c)
        {
            var p = Presets;
            for (int i = 0; i < p.Length; i++) if (Approximately(p[i], c)) return i;
            return -1;
        }

        /// <summary>Slot of a color in any of the themes (-1 = not a palette color).</summary>
        internal static int AnyPaletteSlotOf(Color c)
        {
            foreach (var set in PaletteSets)
                for (int i = 0; i < set.Length; i++) if (Approximately(set[i], c)) return i;
            return -1;
        }

        void Apply()
        {
            if (RuleMode)
            {
                CommitChips();
                EHRules.MarkChanged();
                _done = true;
                Close();
                return;
            }
            if (_targets.Length == 1 && _targets[0] != null && !string.IsNullOrEmpty(_name) && _name != _targets[0].name)
            {
                Undo.RecordObject(_targets[0], "Rename GameObject");
                _targets[0].name = _name;
            }
            Undo.CollapseUndoOperations(_undoGroup);
            _done = true;
            Close();
        }

        void Cancel()
        {
            if (RuleMode)
            {
                _rule.name = _ruleOriginal.name;
                _rule.names = _ruleOriginal.names;
                _rule.nameMatch = _ruleOriginal.nameMatch;
                _rule.components = _ruleOriginal.components;
                _rule.paletteSlot = _ruleOriginal.paletteSlot;
                _rule.paletteSlot2 = _ruleOriginal.paletteSlot2;
                _rule.style.CopyVisualsFrom(_ruleOriginal.style);
                EHRules.MarkChanged();
                _done = true;
                Close();
                return;
            }
            Undo.RevertAllDownToGroup(_undoGroup);
            EHStore.NotifyReverted();
            _done = true;
            Close();
        }

        /// <summary>
        /// Turns the current look into a custom rule (by the object's notable component, else its name),
        /// removes the targets' own styles so the rule shows, and opens the Rules window on it.
        /// </summary>
        void MakeRule()
        {
            var go = _targets[0];
            var rule = EHRules.NewRuleFor(go, _draft);
            rule.paletteSlot = PaletteSlotOf(_draft.color);
            rule.paletteSlot2 = PaletteSlotOf(_draft.color2);
            Undo.RevertAllDownToGroup(_undoGroup);
            EHStore.Clear(_targets);
            // goes into the set in use, so it takes effect right away
            rule.group = EHRules.instance.activeSet;
            EHRules.instance.ListOf(rule.group).Insert(0, rule);
            EHRules.MarkChanged();
            _done = true;
            Close();
            EHRulesWindow.Open(rule);
        }

        /// <summary>Drops the targets' own style so the matching rule shows again.</summary>
        void UseRule()
        {
            Undo.RevertAllDownToGroup(_undoGroup);
            EHStore.Clear(_targets);
            _done = true;
            Close();
        }

        void EditTargetRule()
        {
            var rule = _targetRule;
            Cancel();
            EditorApplication.delayCall += () => OpenRule(rule);
        }

        /// <summary>
        /// Edit Rule closes (keeping the changes, like the X) when another window is clicked.
        /// Stays open for its own helpers: the color picker, pop-ups, and the Hierarchy (for "+ From selected").
        /// </summary>
        void OnLostFocus()
        {
            if (_done) return;
            EditorApplication.delayCall += () =>
            {
                if (this == null || _done) return;
                var f = focusedWindow;
                if (f == null || f == this) return; // nothing took focus (e.g. a file dialog) or came back
                var type = f.GetType().Name;
                if (type == "ColorPicker" || type == "PopupWindow" || type == "ObjectSelector"
                    || type.Contains("Dropdown") || type.Contains("Tooltip")) return;
                if (RuleMode && type.Contains("Hierarchy")) return; // picking an object for "+ From selected"
                Apply();
            };
        }

        void OnDestroy()
        {
            if (_ticking) { _ticking = false; EditorApplication.update -= Tick; }
            if (RuleMode)
            {
                if (!_done) EHRules.MarkChanged(); // closed with X: keep changes
                return;
            }
            if (!_done) Undo.CollapseUndoOperations(_undoGroup); // closed with X: keep changes
        }

        void OnSelectionChange()
        {
            // keep editing the original targets; nothing to do
        }

        // ------------------------------------------------------------------ GUI

        void Styles()
        {
            if (_title != null) return;
            _title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 15, normal = { textColor = TextCol } };
            _sub = new GUIStyle(EditorStyles.label) { fontSize = 11, normal = { textColor = Dim } };
            _section = new GUIStyle(EditorStyles.miniBoldLabel) { fontSize = 10, normal = { textColor = Dim } };
            _field = new GUIStyle(EditorStyles.label)
            {
                fontSize = 13,
                padding = new RectOffset(10, 10, 0, 0),
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = TextCol },
                focused = { textColor = TextCol },
                hover = { textColor = TextCol }
            };
            _hexField = new GUIStyle(_field) { fontSize = 12 };
            _barTitle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11, padding = new RectOffset(0, 0, 0, 0) };
            _dimField = new GUIStyle(_field) { normal = { textColor = Dim } };
            _valueRight = new GUIStyle(_section) { alignment = TextAnchor.MiddleRight };
            _gridMessage = new GUIStyle(_sub) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            _placeholder = new GUIStyle(_hexField) { normal = { textColor = EHDraw.WithAlpha(Dim, 0.7f) } };
        }

        // ---- layout (heights in px)
        const float HeaderH = 76f, PreviewH = 60f, BarH = 24f, BarGap = 6f, SectionGap = 10f;
        const float TextBoxH = 124f, ColorBoxH = 186f, IconTopH = 56f, DarkenH = 28f, ExpandH = 34f, FooterH = 56f, MinGridH = 132f;

        bool _textOpen = true, _colorOpen = true, _iconsExpanded;
        Rect _normalRect;
        Vector2 _pageScroll;

        float TextSectionH => BarH + Open(_textT) * (BarGap + TextBoxH) + SectionGap;
        float ColorSectionH => BarH + Open(_colorT) * (BarGap + ColorBoxH) + SectionGap;

        // ---- fold animation: each box slides open / shut; "More icons" also grows the window smoothly
        const float FoldTime = 0.18f, ResizeTime = 0.2f;
        float _textT = 1f, _colorT = 1f;        // 0 folded … 1 open (eased when drawn)
        double _lastTick;
        Rect _resizeFrom, _resizeTo;
        double _resizeStart = -1;
        bool _ticking;

        static float Open(float t) => t * t * (3f - 2f * t); // smoothstep

        void StartAnim()
        {
            if (_ticking) return;
            _ticking = true;
            _lastTick = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
        }

        void Tick()
        {
            if (this == null) { EditorApplication.update -= Tick; return; }
            double now = EditorApplication.timeSinceStartup;
            float step = (float)(now - _lastTick) / FoldTime;
            _lastTick = now;
            _textT = Mathf.MoveTowards(_textT, _textOpen ? 1f : 0f, step);
            _colorT = Mathf.MoveTowards(_colorT, _colorOpen ? 1f : 0f, step);
            bool resizing = _resizeStart >= 0;
            if (resizing)
            {
                float k = Mathf.Clamp01((float)(now - _resizeStart) / ResizeTime);
                float e = Open(k);
                position = new Rect(_resizeTo.x, Mathf.Lerp(_resizeFrom.y, _resizeTo.y, e),
                    position.width, Mathf.Lerp(_resizeFrom.height, _resizeTo.height, e));
                if (k >= 1f) _resizeStart = -1;
            }
            Repaint();
            bool done = Mathf.Approximately(_textT, _textOpen ? 1f : 0f) && Mathf.Approximately(_colorT, _colorOpen ? 1f : 0f) && _resizeStart < 0;
            if (done) { _ticking = false; EditorApplication.update -= Tick; }
        }

        void SetTextOpen(bool open) { _textOpen = open; StartAnim(); }
        void SetColorOpen(bool open) { _colorOpen = open; StartAnim(); }

        void ResizeTo(Rect to)
        {
            _resizeFrom = position;
            _resizeTo = to;
            _resizeStart = EditorApplication.timeSinceStartup;
            StartAnim();
        }

        /// <summary>Draws a box under its fold bar, clipped to how far it has slid open (local y starts at 0).</summary>
        static bool BeginFoldBox(ref float y, float t, float fullH, float pageW, out float shown)
        {
            shown = Open(t) * fullH;
            if (shown < 0.5f) return false;
            GUI.BeginClip(new Rect(0f, y, pageW, shown));
            return true;
        }

        void OnGUI()
        {
            if (_targets == null || _draft == null) { Close(); return; }
            Styles();

            var e = Event.current;
            if (e.type == EventType.MouseMove) Repaint();
            if (HandleObjectPicker(e)) return;
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Escape) { Cancel(); e.Use(); return; }
                if ((e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) &&
                    (GUI.GetNameOfFocusedControl() == NameChipField || GUI.GetNameOfFocusedControl() == CompChipField))
                { CommitChips(); e.Use(); return; }
                if ((e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) &&
                    GUI.GetNameOfFocusedControl() != "eh-icon-search") { Apply(); e.Use(); return; }
            }

            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), Bg);
            if (RuleMode) { OnGUIRule(); return; }

            // everything above the footer scrolls if the window is shorter than its content
            var view = new Rect(0, 0, position.width, position.height - FooterH);
            float fixedH = HeaderH + PreviewH + TextSectionH + ColorSectionH + IconTopH + DarkenH + ExpandH;
            float gridH = Mathf.Max(MinGridH, view.height - fixedH);
            float contentH = fixedH + gridH;
            bool scrolling = contentH > view.height + 0.5f;
            float pageW = scrolling ? position.width - 14f : position.width;
            if (scrolling) _pageScroll = GUI.BeginScrollView(view, _pageScroll, new Rect(0, 0, pageW, contentH));
            else _pageScroll = Vector2.zero;

            float width = pageW - Pad * 2f;
            float y = 14f;

            // ---- header
            var headIcon = EHIcons.Find("Folder Icon");
            if (headIcon != null)
                GUI.DrawTexture(new Rect(Pad, y + 4f, 18f, 18f), headIcon, ScaleMode.ScaleToFit, true, 0f,
                    _draft.hasColor ? _draft.Color1 : TextCol, 0f, 0f);
            GUI.Label(new Rect(Pad + 26f, y, width, 24f),
                _targets.Length == 1 ? "Edit Style" : $"Edit Style  ·  {_targets.Length} objects", _title);
            GUI.Label(new Rect(Pad + 26f, y + 22f, width, 18f),
                _isSeparator ? "Choose a label and accent for this separator." : "Choose a name and visual accent for this object.", _sub);
            y += 50f;
            EHDraw.Rect(new Rect(Pad, y, width, 1f), Divider);
            y = HeaderH;

            // ---- preview
            if (_isSeparator && _allSeparators != null)
            {
                var chip = new Rect(Pad + width - 132f, y - 2f, 132f, 18f);
                bool all = AllSeparators;
                if (SmallChip(chip, all ? "✓ All separators (" + _allSeparators.Length + ")" : "This separator only", all,
                        all ? "On: color / fill / bold / align change every separator in the open scenes (names stay).\nClick to edit only this one."
                            : "Off: only the selected separator. Click to style every separator together."))
                    SetAllSeparators(!all);
            }
            else if (_targetRule != null)
            {
                // which rule styles this object; click to edit it (or to go back to it when overridden)
                var chip = new Rect(Pad + width - 190f, y - 2f, 190f, 18f);
                if (_targetHasOwn)
                {
                    if (SmallChip(chip, "Overrides rule: " + _targetRule.name + "  ·  Use rule", false,
                            "This object has its own style on top of the rule \"" + _targetRule.name + "\".\nClick to remove its own style and use the rule."))
                    { UseRule(); GUIUtility.ExitGUI(); }
                }
                else if (SmallChip(chip, "✓ Rule: " + _targetRule.name + "  ·  Edit", true,
                             "Styled by the rule \"" + _targetRule.name + "\".\nClick to edit the rule (changes every matching object).\nChanging this window gives just this object its own style."))
                { EditTargetRule(); GUIUtility.ExitGUI(); }
            }
            Section(ref y, "PREVIEW");
            DrawPreview(new Rect(Pad, y, width, 30f));
            y = HeaderH + PreviewH;

            const float gp = 8f;
            float gx = Pad + gp, gw = width - gp * 2f;
            float half = (gw - 12f) * 0.5f;

            // ---- TEXT OPTIONS: name, options, separator align
            if (FoldBar(new Rect(Pad, y, width, BarH), "TEXT OPTIONS", _textOpen)) SetTextOpen(!_textOpen);
            y += BarH;
            float textTop = y;
            if (BeginFoldBox(ref y, _textT, BarGap + TextBoxH, pageW, out float textShown))
            {
                y = textShown - (BarGap + TextBoxH); // slides down from under the bar
                y += BarGap;
                var box = new Rect(Pad, y, width, TextBoxH);
                Group(box);
                float gy = y + gp;
                GUI.Label(new Rect(gx, gy, gw, 16f), "NAME", _section);
                var nameRect = new Rect(gx, gy + 20f, gw, 30f);
                EHDraw.Rounded(nameRect, Card, 7f);
                if (_targets.Length == 1)
                    _name = EditorGUI.TextField(nameRect, _name, _field);
                else
                    GUI.Label(nameRect, _multiLabel ?? (_multiLabel = _targets.Length + " objects selected"), _dimField);
                gy += 50f + 10f;

                GUI.Label(new Rect(gx, gy, half, 16f), "OPTIONS", _section);
                GUI.Label(new Rect(gx + half + 12f, gy, half, 16f), _isSeparator ? "SEPARATOR ALIGN" : "SEPARATOR ALIGN  (separators only)", _section);
                gy += 20f;
                float oW = (half - 4f) * 0.5f;
                // separators are always bold, so the option is shown on and locked for them
                if (Segment(new Rect(gx, gy, oW, 28f), "Bold", _draft.bold || _isSeparator, !_isSeparator)) { _draft.bold = !_draft.bold; Push(); }
                if (Segment(new Rect(gx + oW + 4f, gy, oW, 28f), "Tint Children", _draft.tintChildren && _draft.hasColor, _draft.hasColor))
                {
                    _draft.tintChildren = !_draft.tintChildren; Push();
                }
                string[] aligns = { "Auto", "Left", "Center", "Right" };
                float aW = (half - 12f) / 4f;
                for (int i = 0; i < 4; i++)
                {
                    var r = new Rect(gx + half + 12f + i * (aW + 4f), gy, aW, 28f);
                    if (Segment(r, aligns[i], (int)_draft.align == i, _isSeparator)) { _draft.align = (SeparatorAlign)i; Push(); }
                }
                GUI.EndClip();
            }
            y = textTop + textShown + SectionGap;

            // ---- COLOR OPTIONS: fill / gradient, color, gradient end color
            if (FoldBar(new Rect(Pad, y, width, BarH), "COLOR OPTIONS", _colorOpen)) SetColorOpen(!_colorOpen);
            y += BarH;
            float colorTop = y;
            if (BeginFoldBox(ref y, _colorT, BarGap + ColorBoxH, pageW, out float colorShown))
            {
                y = colorShown - (BarGap + ColorBoxH);
                y += BarGap;
                var box = new Rect(Pad, y, width, ColorBoxH);
                Group(box);
                float gy = y + gp;
                GUI.Label(new Rect(gx, gy, half, 16f), "FILL", _section);
                GUI.Label(new Rect(gx + half + 12f, gy, half, 16f), "GRADIENT", _section);
                string[] fills = { "Soft", "Solid", "Outline" };
                string[] grads = { "None", "Fade", "2-Color" };
                float segW = (half - 8f) / 3f;
                for (int i = 0; i < 3; i++)
                {
                    if (Segment(new Rect(gx + i * (segW + 4f), gy + 20f, segW, 28f), fills[i], (int)_draft.fill == i, _draft.hasColor))
                    { _draft.fill = (FillMode)i; Push(); }
                    if (Segment(new Rect(gx + half + 12f + i * (segW + 4f), gy + 20f, segW, 28f), grads[i], (int)_draft.gradient == i, _draft.hasColor))
                    { _draft.gradient = (GradientMode)i; Push(); }
                }
                gy += 48f + 10f;
                ColorRow(gx, gy, gw, true);
                gy += 50f + 10f;
                ColorRow(gx, gy, gw, false);
                GUI.EndClip();
            }
            y = colorTop + colorShown + SectionGap;

            // ---- ICON
            Section(ref y, "ICON");
            float tW = (width - 4f * (IconTabs.Length - 1)) / IconTabs.Length;
            for (int i = 0; i < IconTabs.Length; i++)
            {
                var r = new Rect(Pad + i * (tW + 4f), y, tW, 28f);
                if (Segment(r, IconTabs[i], _iconTab == i, true))
                {
                    _iconTab = i;
                    if (i == 0) SetIcon(IconMode.Default, _draft.icon);
                    else if (i == 1) SetIcon(IconMode.Auto, _draft.icon);
                }
            }
            y += 28f + 8f;
            DrawIconGrid(new Rect(Pad, y, width, gridH));
            y += gridH + 6f;
            DarkenRow(new Rect(Pad, y, width, 18f));
            y += DarkenH - 2f;

            // ---- expand / collapse the icon area
            if (ExpandButton(new Rect(Pad, y, width, 20f), _iconsExpanded)) ToggleIconsExpanded();

            if (scrolling) GUI.EndScrollView();

            // ---- footer (always pinned to the bottom)
            float fy = position.height - FooterH;
            float fw = position.width - Pad * 2f;
            EHDraw.Rect(new Rect(Pad, fy, fw, 1f), Divider);
            fy += 10f;
            if (Segment(new Rect(Pad, fy, 90f, 32f), "Clear", false, true))
            {
                _draft.hasColor = false; _draft.bold = false; _draft.iconMode = IconMode.Default;
                _draft.icon = null; _draft.align = SeparatorAlign.Default; _draft.iconDarken = 0f; _iconTab = 0; Push();
            }
            if (_targets.Length > 0 &&
                Segment(new Rect(Pad + 96f, fy, 96f, 32f), "Make Rule", false, true,
                    "Make a rule from this look: objects like this one (same component, or same name) get it automatically.\nOpens the Rules window to fine-tune it."))
            { MakeRule(); GUIUtility.ExitGUI(); }
            if (Segment(new Rect(Pad + fw - 196f, fy, 90f, 32f), "Cancel", false, true)) { Cancel(); GUIUtility.ExitGUI(); }
            if (Segment(new Rect(Pad + fw - 100f, fy, 100f, 32f), "Apply", true, true)) { Apply(); GUIUtility.ExitGUI(); }
        }

        /// <summary>
        /// ▼: fold both option boxes (preview stays) and grow the window as far as the screen allows.
        /// ▲: unfold them and go back to the previous size.
        /// </summary>
        void ToggleIconsExpanded()
        {
            if (!_iconsExpanded)
            {
                _normalRect = position;
                _iconsExpanded = true;
                SetTextOpen(false);
                SetColorOpen(false);
                var main = EditorGUIUtility.GetMainWindowPosition();
                float newH = Mathf.Max(position.height, Mathf.Min(main.height - 40f, position.height + 320f));
                float y = position.y;
                if (y + newH > main.yMax - 10f) y = Mathf.Max(main.y + 10f, main.yMax - 10f - newH);
                ResizeTo(new Rect(position.x, y, position.width, newH));
            }
            else
            {
                _iconsExpanded = false;
                SetTextOpen(true);
                SetColorOpen(true);
                if (_normalRect.height > 0f)
                    ResizeTo(new Rect(position.x, _normalRect.y, position.width, _normalRect.height));
            }
            _pageScroll = Vector2.zero;
            Repaint();
        }

        bool FoldBar(Rect r, string title, bool open)
        {
            var e = Event.current;
            bool hover = r.Contains(e.mousePosition);
            if (e.type == EventType.Repaint)
            {
                EHDraw.Rounded(r, hover ? CardHover : GroupBg, 8f);
                if (!open) EHDraw.RoundedBorder(r, Divider, 8f, 1f);
                EHDraw.Text(new Rect(r.x + 12f, r.y, r.width - 40f, r.height), title, _barTitle, TextCol, TextAnchor.MiddleLeft);
                EHDraw.Text(new Rect(r.xMax - 30f, r.y, 20f, r.height), open ? "▾" : "▸", EHDraw.Label(true), Dim, TextAnchor.MiddleCenter);
            }
            return GUI.Button(r, Tip(open ? "Click to fold" : "Click to unfold"), GUIStyle.none);
        }

        bool ExpandButton(Rect r, bool expanded)
        {
            var e = Event.current;
            bool hover = r.Contains(e.mousePosition);
            if (e.type == EventType.Repaint)
            {
                EHDraw.Rounded(r, hover ? CardHover : Card, 6f);
                EHDraw.Text(r, expanded ? "▲   Fewer icons" : "▼   More icons", EHDraw.Mini(true),
                    hover ? TextCol : Dim, TextAnchor.MiddleCenter);
            }
            return GUI.Button(r, Tip(expanded ? "Unfold the option boxes and restore the window size"
                : "Fold the option boxes and enlarge the icon area"), GUIStyle.none);
        }

        /// <summary>Lighter rounded box that groups related rows.</summary>
        void Group(Rect r)
        {
            if (Event.current.type != EventType.Repaint) return;
            EHDraw.Rounded(r, GroupBg, 10f);
            EHDraw.RoundedBorder(r, Divider, 10f, 1f);
        }

        /// <summary>COLOR row (primary) or GRADIENT END COLOR row (only active with 2-Color).</summary>
        void ColorRow(float x0, float y0, float w, bool primary)
        {
            bool enabled = primary || (_draft.hasColor && _draft.gradient == GradientMode.TwoColor);
            GUI.Label(new Rect(x0, y0, w, 16f),
                primary ? "COLOR" : enabled ? "GRADIENT END COLOR" : "GRADIENT END COLOR  (choose 2-Color)", _section);
            if (primary && !RuleMode)
            {
                // palette theme switch for both color rows
                const float tabW = 54f, tabGap = 3f;
                float tx = x0 + w - PaletteNames.Length * (tabW + tabGap) + tabGap;
                for (int i = 0; i < PaletteNames.Length; i++)
                {
                    var tab = new Rect(tx + i * (tabW + tabGap), y0 - 2f, tabW, 18f);
                    if (SmallChip(tab, PaletteNames[i], PaletteIndex == i, PaletteTips[i])) { PaletteIndex = i; Repaint(); }
                }
            }
            float y = y0 + 20f, x = x0;
            const float sw = 24f, step = 30f;

            if (primary)
            {
                if (Swatch(new Rect(x, y + 3f, sw, sw), Card, !_draft.hasColor, true)) { _draft.hasColor = false; Push(); }
            }
            else if (SwapButton(new Rect(x, y + 3f, sw, sw), enabled))
            {
                var tmp = _draft.color; _draft.color = _draft.color2; _draft.color2 = tmp;
                _hex = ToHex(_draft.color); _hex2 = ToHex(_draft.color2); Push();
            }
            x += step;

            Color current = primary ? _draft.color : _draft.color2;
            foreach (var preset in Presets)
            {
                // swatches are shown at the current saturation; the stored color stays the full palette color
                var shown = _draft.Tone(preset);
                bool on = enabled && (primary ? _draft.hasColor : true) && Approximately(current, preset);
                if (Swatch(new Rect(x, y + 3f, sw, sw), enabled ? shown : Color.Lerp(Bg, shown, 0.35f), on, false) && enabled)
                    SetColor(primary, preset, true);
                x += step;
            }
            x += 4f;

            using (new EditorGUI.DisabledScope(!enabled))
            {
                var hexRect = new Rect(x, y, x0 + w - x - 54f, 30f);
                EHDraw.Rounded(hexRect, Card, 7f);
                EditorGUI.BeginChangeCheck();
                string hex = EditorGUI.TextField(hexRect, primary ? _hex : _hex2, _hexField);
                if (primary) _hex = hex; else _hex2 = hex;
                if (EditorGUI.EndChangeCheck() && TryHex(hex, out var parsed))
                    SetColor(primary, parsed, false);

                var pickRect = new Rect(x0 + w - 46f, y, 46f, 30f);
                EHDraw.Rounded(pickRect, Card, 7f);
                EditorGUI.BeginChangeCheck();
                var picked = EditorGUI.ColorField(new Rect(pickRect.x + 6f, pickRect.y + 7f, pickRect.width - 12f, 16f),
                    GUIContent.none, current, true, false, false);
                if (EditorGUI.EndChangeCheck())
                    SetColor(primary, EHDraw.Opaque(picked), true);
            }
        }

        /// <summary>Darkens only this object's current icon (0 = as is, 100% = black). Not global.</summary>
        void DarkenRow(Rect r)
        {
            var e = Event.current;
            float value = _draft.iconDarken;
            GUI.Label(new Rect(r.x, r.y + 1f, 96f, 16f), "ICON DARKEN", _section);
            var valueRect = new Rect(r.xMax - 40f, r.y + 1f, 38f, 16f);
            GUI.Label(valueRect, Percent(value), _valueRight);

            var track = new Rect(r.x + 100f, r.center.y - 4f, valueRect.x - 8f - (r.x + 100f), 8f);
            var hit = new Rect(track.x - 8f, r.y, track.width + 16f, r.height);
            int id = GUIUtility.GetControlID(FocusType.Passive, hit);

            if (e.type == EventType.Repaint)
            {
                EHDraw.RoundedTexture(track, EHDraw.Gradient(Color.white, Color.black), 4f);
                var knob = new Rect(track.x + track.width * value - 7f, r.center.y - 7f, 14f, 14f);
                EHDraw.Rounded(knob, Accent, 7f);
                float k = 1f - value;
                EHDraw.Rounded(new Rect(knob.x + 3f, knob.y + 3f, 8f, 8f), new Color(k, k, k, 1f), 4f);
            }

            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (e.button == 0 && hit.Contains(e.mousePosition))
                    {
                        GUIUtility.hotControl = id;
                        SetDarken((e.mousePosition.x - track.x) / track.width);
                        e.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id)
                    {
                        SetDarken((e.mousePosition.x - track.x) / track.width);
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; e.Use(); }
                    break;
            }
        }

        void SetDarken(float v)
        {
            v = Mathf.Clamp01(v);
            if (v < 0.02f) v = 0f; // easy to get back to "off"
            if (Mathf.Approximately(v, _draft.iconDarken)) return;
            _draft.iconDarken = v;
            Push();
        }

        bool SmallChip(Rect r, string label, bool on, string tooltip)
        {
            var e = Event.current;
            bool hover = r.Contains(e.mousePosition);
            if (e.type == EventType.Repaint)
            {
                EHDraw.Rounded(r, on ? Accent : hover ? CardHover : Card, 10f);
                EHDraw.Text(r, label, EHDraw.Mini(true), on ? AccentText : TextCol, TextAnchor.MiddleCenter);
            }
            return GUI.Button(r, Tip(tooltip), GUIStyle.none);
        }

        // ------------------------------------------------------------------ rule mode page

        /// <summary>
        /// Rule page (same for every rule), laid out like Edit Style: RULE (name, name chips, component chips)
        /// and COLOR OPTIONS fold like TEXT / COLOR OPTIONS; the icon area works the same, and "More icons"
        /// folds both boxes (each can be unfolded again).
        /// </summary>
        void OnGUIRule()
        {
            var view = new Rect(0, 0, position.width, position.height - FooterH);
            float innerW = position.width - Pad * 2f - 16f - 14f; // box inner width (minus a possible scrollbar)
            float ruleBoxH = 8f + 60f
                             + 20f + ChipsHeight(_rule.names, innerW) + 10f
                             + 20f + ChipsHeight(_rule.components, innerW) + 8f;
            const float colorBoxH = 8f + 58f + 60f + 56f + 28f + 8f;
            float ruleSectionH = BarH + Open(_textT) * (BarGap + ruleBoxH) + SectionGap;
            float colorSectionH = BarH + Open(_colorT) * (BarGap + colorBoxH) + SectionGap;
            float fixedH = HeaderH + ruleSectionH + colorSectionH + IconTopH + DarkenH + ExpandH;
            float gridH = Mathf.Max(MinGridH, view.height - fixedH);
            float contentH = fixedH + gridH;
            bool scrolling = contentH > view.height + 0.5f;
            float pageW = scrolling ? position.width - 14f : position.width;
            if (scrolling) _pageScroll = GUI.BeginScrollView(view, _pageScroll, new Rect(0, 0, pageW, contentH));
            else _pageScroll = Vector2.zero;

            float width = pageW - Pad * 2f;
            const float gp = 8f;
            float gx = Pad + gp, gw = width - gp * 2f;
            float y = 14f;

            // header
            GUI.Label(new Rect(Pad, y, width, 24f), "Edit Rule", _title);
            GUI.Label(new Rect(Pad, y + 22f, width, 18f), "Objects whose name contains a word, or that have a component, get this look.", _sub);
            y += 50f;
            EHDraw.Rect(new Rect(Pad, y, width, 1f), Divider);
            y = HeaderH;

            // ---- RULE: name, name chips, component chips
            if (FoldBar(new Rect(Pad, y, width, BarH), "RULE", _textOpen)) SetTextOpen(!_textOpen);
            y += BarH;
            float ruleTop = y;
            if (BeginFoldBox(ref y, _textT, BarGap + ruleBoxH, pageW, out float ruleShown))
            {
                y = ruleShown - (BarGap + ruleBoxH);
                y += BarGap;
                var box = new Rect(Pad, y, width, ruleBoxH);
                Group(box);
                float gy = y + gp;
                GUI.Label(new Rect(gx, gy, gw, 16f), "RULE NAME", _section);
                var nameRect = new Rect(gx, gy + 20f, gw, 30f);
                EHDraw.Rounded(nameRect, Card, 7f);
                EditorGUI.BeginChangeCheck();
                var title = EditorGUI.TextField(nameRect, _rule.name, _field);
                if (EditorGUI.EndChangeCheck()) { _rule.name = title; EHRules.MarkChangedLive(); }
                gy += 60f;

                GUI.Label(new Rect(gx, gy, gw, 16f), "NAME CONTAINS  (type a word, then comma or Enter)", _section);
                float h = ChipsHeight(_rule.names, gw);
                Chips(new Rect(gx, gy + 20f, gw, h), _rule.names, v => _rule.names = v, NameChipField, ref _nameInput, "e.g. Manager");
                gy += 20f + h + 10f;

                GUI.Label(new Rect(gx, gy, gw - 110f, 16f), "OR HAS COMPONENT  (Name* = starts with)", _section);
                if (SmallChip(new Rect(gx + gw - 104f, gy - 2f, 104f, 18f), "+ From selected", false,
                        "Add the components of the selected object (Transform and renderers / colliders left out)"))
                    AddSelectedComponents();
                h = ChipsHeight(_rule.components, gw);
                Chips(new Rect(gx, gy + 20f, gw, h), _rule.components, v => _rule.components = v, CompChipField, ref _compInput, "e.g. Light");
                GUI.EndClip();
            }
            y = ruleTop + ruleShown + SectionGap;

            // ---- COLOR OPTIONS: fill / gradient, children, colors
            if (FoldBar(new Rect(Pad, y, width, BarH), "COLOR OPTIONS", _colorOpen)) SetColorOpen(!_colorOpen);
            y += BarH;
            float cTop = y;
            if (BeginFoldBox(ref y, _colorT, BarGap + colorBoxH, pageW, out float cShown))
            {
                y = cShown - (BarGap + colorBoxH);
                y += BarGap;
                var cbox = new Rect(Pad, y, width, colorBoxH);
                Group(cbox);
                float gy = y + gp;
                float half = (gw - 12f) * 0.5f;
                GUI.Label(new Rect(gx, gy, half, 16f), "FILL", _section);
                GUI.Label(new Rect(gx + half + 12f, gy, half, 16f), "GRADIENT", _section);
                string[] fills = { "Soft", "Solid", "Outline" };
                string[] grads = { "None", "Fade", "2-Color" };
                float segW = (half - 8f) / 3f;
                for (int i = 0; i < 3; i++)
                {
                    if (Segment(new Rect(gx + i * (segW + 4f), gy + 20f, segW, 28f), fills[i], (int)_draft.fill == i, _draft.hasColor))
                    { _draft.fill = (FillMode)i; Push(); }
                    if (Segment(new Rect(gx + half + 12f + i * (segW + 4f), gy + 20f, segW, 28f), grads[i], (int)_draft.gradient == i, _draft.hasColor))
                    { _draft.gradient = (GradientMode)i; Push(); }
                }
                gy += 58f;
                ColorRow(gx, gy, gw, true);
                gy += 60f;
                ColorRow(gx, gy, gw, false);
                gy += 56f;
                if (Segment(new Rect(gx, gy, gw, 28f), _draft.tintChildren ? "✓  Color the children too" : "Color the children too",
                        _draft.tintChildren && _draft.hasColor, _draft.hasColor,
                        "Rows under a matching object get a light tint of its color"))
                { _draft.tintChildren = !_draft.tintChildren; Push(); }
                GUI.EndClip();
            }
            y = cTop + cShown + SectionGap;

            // ---- ICON (same as Edit Style)
            Section(ref y, "ICON");
            float tW = (width - 4f * (IconTabs.Length - 1)) / IconTabs.Length;
            for (int i = 0; i < IconTabs.Length; i++)
            {
                var tr = new Rect(Pad + i * (tW + 4f), y, tW, 28f);
                if (Segment(tr, IconTabs[i], _iconTab == i, true))
                {
                    _iconTab = i;
                    if (i == 0) SetIcon(IconMode.Default, _draft.icon);
                    else if (i == 1) SetIcon(IconMode.Auto, _draft.icon);
                }
            }
            y += 28f + 8f;
            DrawIconGrid(new Rect(Pad, y, width, gridH));
            y += gridH + 6f;
            DarkenRow(new Rect(Pad, y, width, 18f));
            y += DarkenH - 2f;
            if (ExpandButton(new Rect(Pad, y, width, 20f), _iconsExpanded)) ToggleIconsExpanded();

            if (scrolling) GUI.EndScrollView();

            // footer
            float fy = position.height - FooterH;
            float fw = position.width - Pad * 2f;
            EHDraw.Rect(new Rect(Pad, fy, fw, 1f), Divider);
            fy += 10f;
            if (Segment(new Rect(Pad + fw - 196f, fy, 90f, 32f), "Cancel", false, true)) { Cancel(); GUIUtility.ExitGUI(); }
            if (Segment(new Rect(Pad + fw - 100f, fy, 100f, 32f), "Apply", true, true)) { Apply(); GUIUtility.ExitGUI(); }
        }

        /// <summary>
        /// Rules window preview: a matching object; under it a match of the same rule, a match of another rule
        /// (both shown per <paramref name="nested"/>), and objects that don't match (tinted when the rule colors
        /// its children). Without a rule, neutral names and the beige accent.
        /// </summary>
        internal static void DrawRuleTree(Rect r, StyleRule rule, ObjectStyle other, NestedMode nested)
        {
            if (Event.current.type != EventType.Repaint) return;
            EHDraw.Rounded(r, EHTheme.Card, 7f);
            var s = EHSettings.instance;
            Color rowBg = EHDraw.WindowBg;
            var st = rule.style;
            bool sample = string.IsNullOrEmpty(rule.id);
            string top = sample ? "Match object" : rule.name;
            string same = sample ? "Match" : rule.name + " 2";

            var tex = st.iconMode == IconMode.Custom ? EHIcons.Find(st.icon)
                : !string.IsNullOrWhiteSpace(rule.components) ? EHIcons.Find(FirstWord(rule.components) + " Icon")
                : null;
            // type icons straight from Unity (always there, easy to see)
            var plainIcon = TypeIcon(typeof(Transform));                         // skipped / no match
            var topIcon = tex ?? EHIcons.Find("Prefab Icon") ?? TypeIcon(typeof(GameObject)); // the sample rule
            // "Other match": the white "Debug On" bug (icon sheet 02, row 12, col 4), else the camera
            var otherIcon = EHIcons.Find("res:icons/toolbars/debug on.png") ?? TypeIcon(typeof(Camera));
            const int rows = 5;
            float rowH = Mathf.Floor((r.height - 8f) / rows);
            int[] depths = { 0, 1, 1, 1, 2 };

            // background rows first, then tree lines, so the lines sit under the looks like in the Hierarchy
            for (int i = 0; i < rows; i++)
                EHDraw.Rounded(new Rect(r.x + 6f, r.y + 4f + i * rowH, r.width - 12f, rowH - 2f), rowBg, 4f);
            if (s.treeLines)
            {
                Color lc = s.treeLineColor;
                float th = s.treeLineThickness;
                Func<int, float> Cy = i => r.y + 4f + i * rowH + (rowH - 2f) * 0.5f;
                Func<int, float> ArrowX = depth => r.x + 6f + 9f + depth * 14f + s.treeLineOffset;
                // parent (row 0) → rows 1..3, "No match" (row 3) → row 4
                float x0 = ArrowX(0);
                EHDraw.Rect(new Rect(x0 - th * 0.5f, Cy(0) + 5f, th, Cy(3) - Cy(0) - 5f), lc);
                for (int i = 1; i <= 3; i++)
                    EHDraw.Rect(new Rect(x0, Cy(i) - th * 0.5f, i == 3 ? 6f : 13f, th), lc);
                float x1 = ArrowX(1);
                EHDraw.Rect(new Rect(x1 - th * 0.5f, Cy(3) + 5f, th, Cy(4) - Cy(3) - 5f), lc);
                EHDraw.Rect(new Rect(x1, Cy(4) - th * 0.5f, 13f, th), lc);
            }

            for (int i = 0; i < rows; i++)
            {
                var row = new Rect(r.x + 6f, r.y + 4f + i * rowH, r.width - 12f, rowH - 2f);
                int depth = depths[i];
                // foldout arrow on the rows that have children (opened)
                if (i == 0 || i == 3)
                    EHDraw.Text(new Rect(row.x + 3f + depth * 14f, row.y, 12f, row.height), "▼", EHDraw.Mini(false),
                        EHDraw.WithAlpha(EHDraw.DefaultText, 0.8f), TextAnchor.MiddleCenter);
                var area = new Rect(row.x + 18f + depth * 14f, row.y, row.width - 18f - depth * 14f - 84f, row.height);
                string tag;
                if (i == 0)
                {
                    DrawRowLook(area, st, top, topIcon, rowBg, null);
                    tag = "match";
                }
                else if (i == 1 || i == 2)
                {
                    var look = i == 1 ? st : other;
                    var icon = i == 1 ? topIcon : otherIcon ?? plainIcon;
                    string label = i == 1 ? same : "Other match";
                    tag = NestedRow(area, look, st, label, icon, plainIcon, rowBg, s, nested);
                }
                else
                {
                    TintBehind(area, st, depth, rowBg, s);
                    DrawRowLook(area, PlainStyle, "No match", plainIcon, rowBg, null);
                    tag = "no match";
                }
                EHDraw.Text(new Rect(row.xMax - 84f, row.y, 80f, row.height), tag, EHDraw.Mini(false),
                    tag == "no match" ? EHDraw.WithAlpha(EHTheme.Dim, 0.7f) : EHTheme.Point, TextAnchor.MiddleRight);
            }
        }

        /// <summary>One match inside the matched parent, drawn the way <paramref name="nested"/> says.</summary>
        static string NestedRow(Rect area, ObjectStyle look, ObjectStyle parent, string label, Texture icon, Texture plainIcon,
            Color rowBg, EHSettings s, NestedMode nested)
        {
            switch (nested)
            {
                case NestedMode.MatchAll:
                    DrawRowLook(area, look, label, icon, rowBg, null);
                    return "match";
                case NestedMode.IconOnly:
                    TintBehind(area, parent, 1, rowBg, s);
                    DrawRowLook(area, Bare(look), label, icon, rowBg, null); // keeps the rule's icon darken
                    return "icon only";
                case NestedMode.Tinted:
                    // its own color as a light child tint, with its own icon (darkened like the rule says)
                    if (look.hasColor && s.childColors)
                        EHDraw.Rounded(new Rect(area.x, area.y + 1f, area.width - 2f, area.height - 2f),
                            EHDraw.WithAlpha(EHDraw.Opaque(look.Color1), s.childTintAlpha), s.rowRadius);
                    DrawRowLook(area, Bare(look, look.bold), label, icon, rowBg, null);
                    return "own tint";
                default:
                    TintBehind(area, parent, 1, rowBg, s);
                    DrawRowLook(area, PlainStyle, label, plainIcon, rowBg, null);
                    return "skipped";
            }
        }

        static Texture TypeIcon(Type t) => EditorGUIUtility.ObjectContent(null, t).image;

        static readonly ObjectStyle PlainStyle = new ObjectStyle();
        static readonly ObjectStyle BareStyle = new ObjectStyle();

        /// <summary>No color (bold only if asked), but the same icon darken (reused, no allocation per repaint).</summary>
        static ObjectStyle Bare(ObjectStyle look, bool bold = false)
        {
            BareStyle.iconDarken = look.iconDarken;
            BareStyle.bold = bold;
            return BareStyle;
        }

        /// <summary>The light tint children get from a colored parent (same math as the Hierarchy).</summary>
        static void TintBehind(Rect area, ObjectStyle parent, int dist, Color rowBg, EHSettings s)
        {
            if (!parent.hasColor || !parent.tintChildren || !s.childColors) return;
            float a = s.childTintAlpha * Mathf.Pow(s.childTintFade, Mathf.Max(0, dist - 1));
            EHDraw.Rounded(new Rect(area.x, area.y + 1f, area.width - 2f, area.height - 2f),
                EHDraw.WithAlpha(EHDraw.Opaque(parent.Color1), a), s.rowRadius);
        }

        static string FirstWord(string list)
        {
            if (string.IsNullOrWhiteSpace(list)) return null;
            foreach (var p in list.Split(','))
                if (p.Trim().Length > 0) return p.Trim().TrimEnd('*');
            return null;
        }

        // ---- chips: each word is a chip (× removes it); typing a comma or Enter makes a new one

        const string NameChipField = "eh-chip-names", CompChipField = "eh-chip-components";
        const float ChipH = 22f, ChipGap = 4f, ChipInputMinW = 110f;
        string _nameInput = "", _compInput = "";
        static readonly GUIContent ChipContent = new GUIContent();

        static List<string> Parts(string list)
        {
            var result = new List<string>();
            foreach (var p in (list ?? "").Split(','))
                if (p.Trim().Length > 0) result.Add(p.Trim());
            return result;
        }

        static float ChipWidth(string text)
        {
            ChipContent.text = text;
            return EHDraw.Mini(true).CalcSize(ChipContent).x + 26f;
        }

        /// <summary>Lays the chips out in rows (plus the input field at the end).</summary>
        static List<Rect> LayoutChips(List<string> parts, float width, out Rect input)
        {
            var rects = new List<Rect>(parts.Count);
            float x = 0f, y = 0f;
            foreach (var p in parts)
            {
                float w = Mathf.Min(ChipWidth(p), width);
                if (x > 0f && x + w > width) { x = 0f; y += ChipH + ChipGap; }
                rects.Add(new Rect(x, y, w, ChipH));
                x += w + ChipGap;
            }
            if (x > 0f && x + ChipInputMinW > width) { x = 0f; y += ChipH + ChipGap; }
            input = new Rect(x, y, width - x, ChipH);
            return rects;
        }

        static float ChipsHeight(string list, float width)
        {
            LayoutChips(Parts(list), width, out var input);
            return input.yMax;
        }

        void Chips(Rect area, string list, Action<string> set, string field, ref string typedText, string hint)
        {
            var parts = Parts(list);
            var rects = LayoutChips(parts, area.width, out var input);
            var e = Event.current;
            int remove = -1;
            for (int i = 0; i < parts.Count; i++)
            {
                var r = rects[i];
                r.x += area.x; r.y += area.y;
                var x = new Rect(r.xMax - 18f, r.y + 3f, 16f, 16f);
                bool hoverX = x.Contains(e.mousePosition);
                if (e.type == EventType.Repaint)
                {
                    EHDraw.Rounded(r, Accent, ChipH * 0.5f);
                    EHDraw.Text(new Rect(r.x + 9f, r.y, r.width - 26f, r.height), parts[i], EHDraw.Mini(true), AccentText, TextAnchor.MiddleLeft);
                    if (hoverX) EHDraw.Rounded(x, EHDraw.WithAlpha(AccentText, 0.18f), 8f);
                    EHDraw.Text(x, "×", EHDraw.Label(true), AccentText, TextAnchor.MiddleCenter);
                }
                if (GUI.Button(x, Tip("Remove"), GUIStyle.none)) remove = i;
            }
            if (remove >= 0)
            {
                parts.RemoveAt(remove);
                set(string.Join(", ", parts));
                EHRules.MarkChangedLive();
                GUIUtility.ExitGUI();
            }

            input.x += area.x; input.y += area.y;
            EHDraw.Rounded(input, Card, ChipH * 0.5f);
            // Backspace on an empty field takes the last chip back
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Backspace && GUI.GetNameOfFocusedControl() == field &&
                string.IsNullOrEmpty(typedText) && parts.Count > 0)
            {
                typedText = parts[parts.Count - 1];
                parts.RemoveAt(parts.Count - 1);
                set(string.Join(", ", parts));
                EHRules.MarkChangedLive();
                e.Use();
            }
            GUI.SetNextControlName(field);
            var typed = EditorGUI.TextField(new Rect(input.x + 4f, input.y + 1f, input.width - 8f, input.height - 2f), typedText, _hexField);
            if (typed != typedText)
            {
                typedText = typed;
                if (typedText.Contains(",")) CommitChips();
            }
            if (string.IsNullOrEmpty(typedText) && e.type == EventType.Repaint && GUI.GetNameOfFocusedControl() != field)
                GUI.Label(new Rect(input.x + 4f, input.y, input.width - 8f, input.height), parts.Count == 0 ? hint : "+ add", _placeholder);
        }

        /// <summary>Turns whatever is typed in either chip field into chips.</summary>
        void CommitChips()
        {
            _rule.names = Merge(_rule.names, _nameInput);
            _rule.components = Merge(_rule.components, _compInput);
            _nameInput = _compInput = "";
            EHRules.MarkChangedLive();
            Repaint();
        }

        static string Merge(string list, string typed)
        {
            var parts = Parts(list);
            foreach (var p in (typed ?? "").Split(','))
            {
                var t = p.Trim();
                if (t.Length > 0 && !parts.Exists(x => string.Equals(x, t, StringComparison.OrdinalIgnoreCase))) parts.Add(t);
            }
            return string.Join(", ", parts);
        }

        static readonly HashSet<string> CommonComponents = new HashSet<string>
        {
            "Transform", "RectTransform", "MeshFilter", "MeshRenderer", "SkinnedMeshRenderer", "CanvasRenderer",
            "BoxCollider", "SphereCollider", "CapsuleCollider", "MeshCollider", "Rigidbody", "LODGroup"
        };

        void AddSelectedComponents()
        {
            var go = Selection.activeGameObject;
            if (go == null) return;
            var have = new List<string>();
            foreach (var p in (_rule.components ?? "").Split(','))
                if (p.Trim().Length > 0) have.Add(p.Trim());
            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null) continue;
                var n = c.GetType().Name;
                if (CommonComponents.Contains(n) || have.Contains(n)) continue;
                have.Add(n);
            }
            _rule.components = string.Join(", ", have);
            GUI.FocusControl(null);
            EHRules.MarkChangedLive();
        }

        void SetColor(bool primary, Color c, bool updateHex)
        {
            if (primary)
            {
                _draft.hasColor = true; _draft.color = c;
                if (updateHex) _hex = ToHex(c);
            }
            else
            {
                _draft.color2 = c;
                if (updateHex) _hex2 = ToHex(c);
            }
            Push();
        }

        void Section(ref float y, string label)
        {
            GUI.Label(new Rect(Pad, y, 200f, 16f), label, _section);
            y += 20f;
        }

        // ------------------------------------------------------------------ pieces

        /// <summary>Change the icon; the darken slider only belongs to the icon it was set for.</summary>
        void SetIcon(IconMode mode, string key)
        {
            bool changed = _draft.iconMode != mode || _draft.icon != key;
            _draft.iconMode = mode;
            _draft.icon = key;
            if (changed) _draft.iconDarken = 0f;
            Push();
        }

        void RemoveCustomIcon(string key)
        {
            var settings = EHSettings.instance;
            settings.customIcons.Remove(key.Substring(EHIcons.AssetPrefix.Length));
            settings.SaveSettings();
            if (_draft.iconMode == IconMode.Custom && _draft.icon == key)
            {
                SetIcon(IconMode.Default, null);
            }
            _filteredIcons = null;
            Repaint();
        }

        void DrawPreview(Rect r)
        {
            if (Event.current.type != EventType.Repaint) return;
            EHDraw.Rounded(r, Card, 7f);
            var s = EHSettings.instance;
            var go = _targets.Length > 0 ? _targets[0] : null;
            if (go == null && !RuleMode) return;

            Color rowBg = EHDraw.WindowBg;
            var row = new Rect(r.x + 8f, r.y + 6f, r.width - 16f, 18f);
            EHDraw.Rounded(row, rowBg, 4f);

            if (_isSeparator)
            {
                DrawSeparatorPreview(row, rowBg, s, go);
                return;
            }
            string label = RuleMode ? _rule.name : (_name ?? go.name);
            var tex = go != null ? EHIcons.Resolve(go, _draft, false)
                : _draft.iconMode == IconMode.Custom ? EHIcons.Find(_draft.icon) : EHIcons.Find("GameObject Icon");
            DrawRowLook(new Rect(row.x + 22f, row.y, row.width - 22f, row.height), _draft, label, tex, rowBg, null);
        }

        /// <summary>
        /// A Hierarchy row's look (fill, gradient, outline, accent bar, icon, name) inside <paramref name="area"/>.
        /// Used by this window's preview and by the Rules list, so both look exactly like the Hierarchy.
        /// </summary>
        internal static void DrawRowLook(Rect area, ObjectStyle st, string label, Texture icon, Color rowBg, string note)
        {
            var s = EHSettings.instance;
            var pill = new Rect(area.x, area.y + 1f, area.width - 2f, area.height - 2f);
            Color text = EHDraw.DefaultText;
            if (st.hasColor)
            {
                Color c = EHDraw.Opaque(st.Color1);
                Color fa = EHHierarchyGUI.FillColor(c, st.fill, rowBg);
                Color fb = st.gradient == GradientMode.TwoColor
                    ? EHHierarchyGUI.FillColor(EHDraw.Opaque(st.Color2), st.fill, rowBg) : rowBg;
                if (st.gradient == GradientMode.None) EHDraw.Rounded(pill, fa, s.rowRadius);
                else EHDraw.RoundedTexture(pill, EHDraw.Gradient(fa, fb), s.rowRadius);
                if (st.fill != FillMode.Solid)
                    EHDraw.RoundedBorder(pill, EHDraw.WithAlpha(c, st.fill == FillMode.Outline ? 0.9f : 0.45f), s.rowRadius, 1f);
                if (s.accentBar)
                    EHDraw.Rounded(new Rect(pill.x, pill.y, 3f, pill.height),
                        st.fill == FillMode.Solid ? EHDraw.Lighten(c, 0.45f) : c, 1.5f);
                text = st.fill == FillMode.Solid ? EHDraw.ContrastText(fa) : EHDraw.TintedText(c, fa);
            }
            float iconSize = Mathf.Min(16f, area.height - 2f);
            if (icon != null)
                GUI.DrawTexture(new Rect(pill.x + 4f, area.center.y - iconSize * 0.5f, iconSize, iconSize), icon, ScaleMode.ScaleToFit,
                    true, 0f, st.IconTint, 0f, 0f);
            var labelStyle = EHDraw.Label(st.bold);
            var textRect = new Rect(pill.x + 24f, area.y, pill.width - 28f, area.height);
            EHDraw.Text(textRect, label, labelStyle, text, TextAnchor.MiddleLeft);
            if (!string.IsNullOrEmpty(note))
            {
                NoteContent.text = label;
                float w = labelStyle.CalcSize(NoteContent).x + 10f;
                if (w < textRect.width - 20f)
                    EHDraw.Text(new Rect(textRect.x + w, area.y, textRect.width - w, area.height), note, EHDraw.Mini(false),
                        EHDraw.WithAlpha(text, 0.6f), TextAnchor.MiddleLeft);
            }
        }

        static readonly GUIContent NoteContent = new GUIContent();

        void DrawSeparatorPreview(Rect row, Color rowBg, EHSettings s, GameObject go)
        {
            string label = (_name ?? go.name).Trim(" -=/_*#~.|".ToCharArray());
            var align = _draft.align != SeparatorAlign.Default ? _draft.align : s.separatorAlign;
            if (align == SeparatorAlign.Default) align = SeparatorAlign.Center;

            bool own = _draft.hasColor;
            Color c = own ? EHDraw.Opaque(_draft.Color1) : EHDraw.Opaque(s.outlineColor);
            Color line = own ? EHDraw.WithAlpha(c, 0.9f) : s.outlineColor;

            var inner = new Rect(row.x + 6f, row.y, row.width - 12f, row.height);
            var st = EHDraw.Label(true);
            float tw = label.Length > 0 ? EHDraw.TextWidth(label, st) : 0f;
            float tx = align == SeparatorAlign.Left ? inner.x + 8f
                : align == SeparatorAlign.Right ? inner.xMax - 8f - tw
                : inner.center.x - tw * 0.5f;

            Color textColor;
            if (own && _draft.fill == FillMode.Solid)
            {
                var pill = new Rect(inner.x, row.y + 1f, inner.width, row.height - 2f);
                Color fb = _draft.gradient == GradientMode.TwoColor ? EHDraw.Opaque(_draft.Color2)
                    : _draft.gradient == GradientMode.Fade ? rowBg : c;
                if (_draft.gradient == GradientMode.None) EHDraw.Rounded(pill, c, s.rowRadius);
                else EHDraw.RoundedTexture(pill, EHDraw.Gradient(c, fb), s.rowRadius);
                textColor = EHDraw.ContrastText(c);
            }
            else
            {
                var box = new Rect(inner.x, row.y + 1f, inner.width, row.height - 2f);
                Color under = rowBg;
                if (own && _draft.fill == FillMode.Soft)
                {
                    under = Color.Lerp(rowBg, c, EditorGUIUtility.isProSkin ? 0.30f : 0.38f);
                    EHDraw.Rounded(box, under, s.rowRadius);
                }
                else if (own && _draft.fill == FillMode.Outline)
                {
                    EHDraw.RoundedBorder(box, EHDraw.WithAlpha(c, 0.9f), s.rowRadius, 1f);
                }
                float y = row.center.y;
                if (tw > 0f)
                {
                    EHDraw.HLine(inner.x, tx - 6f, y, line);
                    EHDraw.HLine(tx + tw + 6f, inner.xMax, y, line);
                }
                else EHDraw.HLine(inner.x, inner.xMax, y, line);
                textColor = own ? EHDraw.TintedText(c, under) : EHDraw.ContrastText(under);
            }
            if (tw > 0f)
                EHDraw.Text(new Rect(tx, row.y, tw + 2f, row.height), label, st, textColor, TextAnchor.MiddleLeft);
        }

        const string AddTile = "<add>";
        const int PickerId = 0x45480;
        List<string> _filteredIcons;
        string _filteredFor;

        bool _showHidden, _filteredHidden, _othersOpen, _filteredOthers;
        const int IconCols = 9;
        const string BlankTile = "<blank>", OthersTile = "<others>";
        int _othersCount;
        readonly HashSet<string> _hiddenSet = new HashSet<string>();

        List<string> IconEntries()
        {
            string q = (_iconSearch ?? "").Trim();
            if (_filteredIcons != null && _filteredFor == q && _filteredHidden == _showHidden &&
                _filteredOthers == _othersOpen) return _filteredIcons;
            _filteredFor = q;
            _filteredHidden = _showHidden;
            _filteredOthers = _othersOpen;
            _filteredIcons = new List<string>();
            var s = EHSettings.instance;
            _hiddenSet.Clear();
            foreach (var h in s.hiddenIcons) _hiddenSet.Add(h);

            if (_showHidden)
            {
                // only the hidden ones, so they're easy to bring back
                foreach (var key in s.hiddenIcons)
                    if (EHIcons.InAll(key) && Matches(key, q)) _filteredIcons.Add(key);
                return _filteredIcons;
            }
            if (q.Length == 0) _filteredIcons.Add(AddTile);
            foreach (var key in s.customIcons)
                if (Matches(EHIcons.AssetPrefix + key, q)) _filteredIcons.Add(EHIcons.AssetPrefix + key);

            // recommended first; searching looks through everything without sections
            foreach (var key in EHIcons.Recommended)
                if (!_hiddenSet.Contains(key) && Matches(key, q)) _filteredIcons.Add(key);

            bool sections = q.Length == 0 && EHIcons.Recommended.Count > 0;
            int headerAt = -1;
            if (sections)
            {
                while (_filteredIcons.Count % IconCols != 0) _filteredIcons.Add(BlankTile);
                headerAt = _filteredIcons.Count;
                _filteredIcons.Add(OthersTile);
                for (int i = 1; i < IconCols; i++) _filteredIcons.Add(BlankTile);
            }

            _othersCount = 0;
            foreach (var key in EHIcons.All)
            {
                if (_hiddenSet.Contains(key) || EHIcons.IsRecommended(key) || !Matches(key, q)) continue;
                _othersCount++;
                if (!sections || _othersOpen) _filteredIcons.Add(key);
            }
            if (sections && _othersCount == 0) _filteredIcons.RemoveRange(headerAt, _filteredIcons.Count - headerAt);
            return _filteredIcons;
        }

        void SetRecommended(string key, bool add)
        {
            var s = EHSettings.instance;
            string name = EHIcons.DisplayName(key);
            bool builtin = EHIcons.IsBuiltinRecommended(name);
            if (add)
            {
                s.recommendedRemoved.Remove(name);
                if (!builtin && !s.recommendedAdded.Contains(name)) s.recommendedAdded.Add(name);
            }
            else
            {
                s.recommendedAdded.Remove(name);
                if (builtin && !s.recommendedRemoved.Contains(name)) s.recommendedRemoved.Add(name);
            }
            s.SaveSettings();
            EHIcons.InvalidateRecommended();
            _filteredIcons = null;
            Repaint();
        }

        void SetIconHidden(string key, bool hide)
        {
            var list = EHSettings.instance.hiddenIcons;
            if (hide) { if (!list.Contains(key)) list.Add(key); }
            else list.Remove(key);
            EHSettings.instance.SaveSettings();
            if (hide && _draft.iconMode == IconMode.Custom && _draft.icon == key)
            {
                SetIcon(IconMode.Default, null);
            }
            _filteredIcons = null;
            Repaint();
        }

        static bool Matches(string key, string q) =>
            q.Length == 0 || EHIcons.DisplayName(key).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;

        void DrawIconGrid(Rect area)
        {
            var e = Event.current;
            if (_iconTab <= 1)
            {
                if (e.type == EventType.Repaint)
                {
                    EHDraw.Rounded(area, Card, 8f);
                    string msg = _iconTab == 0
                        ? "Unity's default icon (prefab / GameObject)."
                        : "Automatic: uses the icon of the object's main component\n(Camera, Light, AudioSource, your scripts...).";
                    GUI.Label(area, msg, _gridMessage);
                }
                return;
            }

            // search
            int hiddenCount = 0;
            foreach (var h in EHSettings.instance.hiddenIcons)
                if (EHIcons.InAll(h)) hiddenCount++;
            float hiddenW = hiddenCount > 0 || _showHidden ? 86f : 0f;
            var sRect = new Rect(area.x, area.y, area.width - (hiddenW > 0f ? hiddenW + 6f : 0f), 24f);
            if (hiddenW > 0f)
            {
                var hr = new Rect(sRect.xMax + 6f, area.y + 3f, hiddenW, 18f);
                if (SmallChip(hr, _showHidden ? "◀ Back" : "Hidden " + hiddenCount, _showHidden,
                        _showHidden ? "Back to all icons" : "Show hidden icons (right-click one to unhide)"))
                {
                    _showHidden = !_showHidden;
                    _iconScroll = Vector2.zero;
                    _filteredIcons = null;
                }
            }
            EHDraw.Rounded(sRect, Card, 7f);
            GUI.SetNextControlName("eh-icon-search");
            EditorGUI.BeginChangeCheck();
            _iconSearch = EditorGUI.TextField(sRect, _iconSearch, _hexField);
            if (EditorGUI.EndChangeCheck()) _iconScroll = Vector2.zero;
            if (string.IsNullOrEmpty(_iconSearch) && e.type == EventType.Repaint)
                GUI.Label(sRect, "Search icons…", _placeholder);

            // grid
            var entries = IconEntries();
            var grid = new Rect(area.x, area.y + 30f, area.width, area.height - 30f);
            const int cols = IconCols;
            const float rowH = 44f;
            float inner = grid.width - 14f;
            float cell = (inner - (cols - 1) * 4f) / cols;
            int rows = Mathf.CeilToInt(entries.Count / (float)cols);
            if (e.type == EventType.Repaint) EHDraw.Rounded(grid, EHDraw.WithAlpha(Card, 0.5f), 8f);

            bool hidden = _showHidden;

            _iconScroll = GUI.BeginScrollView(grid, _iconScroll, new Rect(0, 0, inner, Mathf.Max(rows * rowH, grid.height)));
            int firstRow = Mathf.Max(0, Mathf.FloorToInt(_iconScroll.y / rowH));
            int lastRow = Mathf.Min(rows - 1, Mathf.CeilToInt((_iconScroll.y + grid.height) / rowH));
            for (int row = firstRow; row <= lastRow; row++)
            for (int col = 0; col < cols; col++)
            {
                int idx = row * cols + col;
                if (idx >= entries.Count) break;
                string key = entries[idx];
                if (key == BlankTile) continue;
                var r = new Rect(col * (cell + 4f), row * rowH + 2f, cell, 40f);
                bool hover = r.Contains(e.mousePosition);

                if (key == OthersTile)
                {
                    // full-width foldout between the recommended icons and everything else
                    var hr = new Rect(0f, row * rowH + 8f, inner, 28f);
                    bool hh = hr.Contains(e.mousePosition);
                    if (e.type == EventType.Repaint)
                    {
                        EHDraw.Rounded(hr, hh ? CardHover : GroupBg, 7f);
                        EHDraw.Text(new Rect(hr.x + 12f, hr.y, hr.width - 24f, hr.height),
                            (_othersOpen ? "▼  OTHER ICONS  (" : "▶  OTHER ICONS  (") + _othersCount + ")",
                            EHDraw.Mini(true), hh ? TextCol : Dim, TextAnchor.MiddleLeft);
                    }
                    if (GUI.Button(hr, Tip(_othersOpen ? "Hide the other built-in icons" : "Show every other built-in icon"), GUIStyle.none))
                    {
                        _othersOpen = !_othersOpen;
                        _filteredIcons = null;
                        Repaint();
                    }
                    break; // rest of this row is blank
                }

                if (key == AddTile)
                {
                    if (e.type == EventType.Repaint)
                    {
                        // accent tile so "add" stands out from the icon grid; fills in on hover
                        EHDraw.Rounded(r, hover ? Point : EHDraw.WithAlpha(Point, 0.16f), 8f);
                        EHDraw.RoundedBorder(r, Point, 8f, 1.5f);
                        if (_plus == null)
                            _plus = new GUIStyle(EditorStyles.boldLabel)
                            {
                                fontSize = 18, alignment = TextAnchor.MiddleCenter,
                                padding = new RectOffset(0, 0, 0, 0)
                            };
                        EHDraw.Text(new Rect(r.x, r.y - 5f, r.width, r.height), "+", _plus, hover ? AccentText : Point, TextAnchor.MiddleCenter);
                        EHDraw.Text(new Rect(r.x, r.yMax - 15f, r.width, 12f), "ADD", EHDraw.Mini(true),
                            hover ? AccentText : Point, TextAnchor.MiddleCenter);
                    }
                    if (GUI.Button(r, Tip("Add your own icon (any Texture in the project)"), GUIStyle.none))
                        EditorGUIUtility.ShowObjectPicker<Texture2D>(null, false, "", PickerId);
                    continue;
                }

                bool isCustom = key.StartsWith(EHIcons.AssetPrefix, StringComparison.Ordinal);
                bool on = _draft.iconMode == IconMode.Custom && _draft.icon == key;
                var removeRect = new Rect(r.xMax - 13f, r.y + 1f, 12f, 12f);
                if (e.type == EventType.Repaint)
                {
                    // selected cell keeps the dark card so light icons stay visible; orange ring marks it
                    Color cellBg = hover || on ? CardHover : Card;
                    EHDraw.Rounded(r, cellBg, 8f);
                    if (on) EHDraw.RoundedBorder(r, Point, 8f, 2f);
                    var tex = EHIcons.Find(key);
                    if (tex != null)
                        GUI.DrawTexture(new Rect(r.center.x - 10f, r.center.y - 10f, 20f, 20f), tex, ScaleMode.ScaleToFit,
                            true, 0f, on ? _draft.IconTint : new Color(1f, 1f, 1f, hidden ? 0.45f : 1f), 0f, 0f);
                    if (isCustom)
                    {
                        if (hover)
                        {
                            bool overX = removeRect.Contains(e.mousePosition);
                            EHDraw.Rounded(removeRect, overX ? new Color(0.9f, 0.32f, 0.34f, 1f) : new Color(0.55f, 0.25f, 0.26f, 1f), 6f);
                            EHDraw.Text(new Rect(removeRect.x, removeRect.y - 1f, removeRect.width, removeRect.height), "×",
                                EHDraw.Mini(true), Color.white, TextAnchor.MiddleCenter);
                        }
                        else EHDraw.Rounded(new Rect(r.xMax - 7f, r.y + 3f, 4f, 4f), Point, 2f);
                    }
                }

                // remove one of my icons: hover × (left click) or right-click menu.
                // Handled on MouseDown before GUI.Button so the button can't swallow it
                // (ContextClick never reaches this utility window reliably).
                if (isCustom && hover && e.type == EventType.MouseDown &&
                    ((e.button == 0 && removeRect.Contains(e.mousePosition)) || e.button == 1))
                {
                    string removeKey = key;
                    if (e.button == 0) RemoveCustomIcon(removeKey);
                    else
                    {
                        var menu = new GenericMenu();
                        menu.AddItem(new GUIContent("Remove from my icons"), false, () => RemoveCustomIcon(removeKey));
                        menu.ShowAsContext();
                    }
                    e.Use();
                    continue;
                }

                // built-in icon: right-click to hide it from the All tab (or bring it back)
                if (!isCustom && hover && e.type == EventType.MouseDown && e.button == 1)
                {
                    string iconKey = key;
                    var menu = new GenericMenu();
                    if (hidden) menu.AddItem(new GUIContent("Unhide this icon"), false, () => SetIconHidden(iconKey, false));
                    else
                    {
                        if (EHIcons.IsRecommended(iconKey))
                            menu.AddItem(new GUIContent("Remove from Recommended"), false, () => SetRecommended(iconKey, false));
                        else
                            menu.AddItem(new GUIContent("Add to Recommended"), false, () => SetRecommended(iconKey, true));
                        menu.AddSeparator("");
                        menu.AddItem(new GUIContent("Hide this icon"), false, () => SetIconHidden(iconKey, true));
                    }
                    menu.ShowAsContext();
                    e.Use();
                    continue;
                }

                if (GUI.Button(r, Tip(EHIcons.Tooltip(key, isCustom)), GUIStyle.none))
                {
                    SetIcon(IconMode.Custom, key);
                }
            }
            GUI.EndScrollView();
        }

        /// <summary>Receives the texture chosen with the + tile and stores it as a custom icon.</summary>
        bool HandleObjectPicker(Event e)
        {
            if (e.type != EventType.ExecuteCommand || e.commandName != "ObjectSelectorClosed") return false;
            if (EditorGUIUtility.GetObjectPickerControlID() != PickerId) return false;

            var tex = EditorGUIUtility.GetObjectPickerObject() as Texture2D;
            if (tex != null)
            {
                var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(tex));
                if (!string.IsNullOrEmpty(guid))
                {
                    var customs = EHSettings.instance.customIcons;
                    if (!customs.Contains(guid)) { customs.Insert(0, guid); EHSettings.instance.SaveSettings(); }
                    _filteredIcons = null;
                    SetIcon(IconMode.Custom, EHIcons.AssetPrefix + guid);
                }
            }
            e.Use();
            return true;
        }

        bool Segment(Rect r, string label, bool on, bool enabled, string tooltip)
        {
            bool clicked = Segment(r, label, on, enabled);
            if (Event.current.type == EventType.Repaint) GUI.Label(r, Tip(tooltip), GUIStyle.none); // tooltip only
            return clicked;
        }

        bool Segment(Rect r, string label, bool on, bool enabled)
        {
            var e = Event.current;
            bool hover = enabled && r.Contains(e.mousePosition);
            if (e.type == EventType.Repaint)
            {
                Color bg = on ? Accent : hover ? CardHover : Card;
                if (!enabled) bg = Color.Lerp(Bg, bg, 0.45f);
                EHDraw.Rounded(r, bg, 7f);
                Color tc = on ? AccentText : enabled ? TextCol : EHDraw.WithAlpha(Dim, 0.6f);
                EHDraw.Text(r, label, EHDraw.Label(on), tc, TextAnchor.MiddleCenter);
            }
            return enabled && GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        bool SwapButton(Rect r, bool enabled)
        {
            var e = Event.current;
            bool hover = enabled && r.Contains(e.mousePosition);
            if (e.type == EventType.Repaint)
            {
                EHDraw.Rounded(r, hover ? CardHover : Card, r.height * 0.5f);
                EHDraw.RoundedBorder(r, EHDraw.WithAlpha(Dim, enabled ? 0.6f : 0.25f), r.height * 0.5f, 1f);
                EHDraw.Text(r, "⇄", EHDraw.Label(true), enabled ? TextCol : EHDraw.WithAlpha(Dim, 0.5f), TextAnchor.MiddleCenter);
            }
            return enabled && GUI.Button(r, Tip("Swap start / end colors"), GUIStyle.none);
        }

        bool Swatch(Rect r, Color c, bool on, bool isNone)
        {
            var e = Event.current;
            bool hover = r.Contains(e.mousePosition);
            if (e.type == EventType.Repaint)
            {
                if (on)
                {
                    var ring = new Rect(r.x - 3f, r.y - 3f, r.width + 6f, r.height + 6f);
                    EHDraw.Rounded(ring, Point, ring.height * 0.5f);
                    EHDraw.Rounded(new Rect(r.x - 1f, r.y - 1f, r.width + 2f, r.height + 2f), Bg, (r.height + 2f) * 0.5f);
                }
                else if (hover)
                {
                    var ring = new Rect(r.x - 2f, r.y - 2f, r.width + 4f, r.height + 4f);
                    EHDraw.RoundedBorder(ring, EHDraw.WithAlpha(TextCol, 0.4f), ring.height * 0.5f, 1.5f);
                }
                EHDraw.Rounded(r, c, r.height * 0.5f);
                if (isNone)
                {
                    EHDraw.RoundedBorder(r, EHDraw.WithAlpha(Dim, 0.6f), r.height * 0.5f, 1f);
                    EHDraw.Text(r, "×", EHDraw.Label(true), Dim, TextAnchor.MiddleCenter);
                }
            }
            return GUI.Button(r, Tip(isNone ? "No color" : PresetHex(c)), GUIStyle.none);
        }

        // ------------------------------------------------------------------ color utils

        /// <summary>Hex label for a swatch; palette colors are formatted once.</summary>
        static string PresetHex(Color c)
        {
            if (!PresetHexCache.TryGetValue(c, out var hex))
            {
                hex = ToHex(c);
                if (PresetHexCache.Count > 256) PresetHexCache.Clear();
                PresetHexCache[c] = hex;
            }
            return hex;
        }

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.gray;

        static string ToHex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        static bool TryHex(string text, out Color c)
        {
            c = default;
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.Trim();
            if (!text.StartsWith("#")) text = "#" + text;
            if (text.Length != 7) return false;
            if (!ColorUtility.TryParseHtmlString(text, out c)) return false;
            c.a = 1f;
            return true;
        }

        static bool Approximately(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 0.004f && Mathf.Abs(a.g - b.g) < 0.004f && Mathf.Abs(a.b - b.b) < 0.004f;
    }
}
