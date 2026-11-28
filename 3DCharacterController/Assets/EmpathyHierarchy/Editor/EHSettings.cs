using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EmpathyHierarchy
{
    public enum FillMode { Soft, Solid, Outline }
    public enum GradientMode { None, Fade, TwoColor }
    public enum IconMode { Default, Auto, Custom }
    public enum SeparatorAlign { Default, Left, Center, Right }
    public enum RootOutlineMode { Off, ColoredRoots, AllRoots }
    public enum WidgetColorMode { Subtle, Accent, Custom }
    public enum ActiveToggleStyle { Checkbox, Switch, Dot }
    public enum ArrowTintMode { Off, RootsAndSeparators, AllColored }
    public enum TagLayerStyle { Off, Combined, Circles }
    public enum ChildCountMode { Direct, Total, Both, Smart }
    public enum NavBarMode { Bar, Tab }

    [Serializable]
    public class NamedColor
    {
        public string name;
        public Color color = new Color(0.36f, 0.43f, 0.58f, 1f);
    }

    /// <summary>
    /// Project-wide settings, saved to ProjectSettings/EmpathyHierarchy/Settings.asset
    /// so the whole team shares the same look through version control.
    /// </summary>
    [FilePath("ProjectSettings/EmpathyHierarchy/Settings.asset", FilePathAttribute.Location.ProjectFolder)]
    public class EHSettings : ScriptableSingleton<EHSettings>
    {
        /// <summary>Checks the saved file (backup, script link) before it is read.</summary>
        public new static EHSettings instance
        {
            get { EHDataGuard.Ensure(); return ScriptableSingleton<EHSettings>.instance; }
        }

        [Header("General")]
        public bool enabled = true;
        [Range(0f, 8f)] public float rowRadius = 5f;
        [Tooltip("Extra space kept free at the right edge (e.g. for the prefab arrow).")]
        [Range(0f, 40f)] public float rightPadding = 0f;
        public bool boldRootObjects = false;
        [Tooltip("Show the main component's icon for every object, not just styled ones.")]
        public bool componentAwareIcons = false;

        [Header("Striped Rows")]
        public bool stripedRows = true;
        [Range(0f, 0.15f)] public float stripeStrength = 0.035f;

        [Header("Tree Lines")]
        public bool treeLines = true;
        public Color treeLineColor = new Color(0.55f, 0.55f, 0.55f, 0.55f);
        [Range(1f, 3f)] public float treeLineThickness = 1f;
        [Tooltip("Lines under a colored parent use that parent's color (thicker).")]
        public bool branchColoredLines = true;
        [Tooltip("Nudge tree lines horizontally (pixels) if they don't line up with the foldout arrows.")]
        [Range(-8f, 8f)] public float treeLineOffset = 0f;

        [Header("Child Colors")]
        public bool childColors = true;
        [Range(0f, 0.6f)] public float childTintAlpha = 0.22f;
        [Range(0.3f, 1f)] public float childTintFade = 0.7f;

        [Header("Accents & Outlines")]
        [Tooltip("Thin vertical bar on the left of colored rows.")]
        public bool accentBar = true;
        public RootOutlineMode rootOutline = RootOutlineMode.ColoredRoots;
        [Tooltip("New Hierarchy: root objects and separators fill under the foldout arrow, and arrows take the row's color.")]
        public bool extendRootRows = true;
        [Tooltip("New Hierarchy: which foldout arrows take their row's color. Everything else keeps Unity's default arrow.")]
        public ArrowTintMode arrowTint = ArrowTintMode.RootsAndSeparators;
        [Tooltip("Off: outline lines stop at the rounded row ends. On: they run across the whole window width.")]
        public bool outlineFullWidth = false;
        [Tooltip("Foldout arrows show their state. Filled triangle = folded (hidden children), hollow = unfolded.")]
        public bool arrowBadges = true;
        [Tooltip("Brighter foldout arrows (white on dark rows, dark on light rows).")]
        public bool brightArrows = true;
        public Color outlineColor = new Color(0.55f, 0.55f, 0.55f, 0.8f);

        [Header("Separators")]
        [Tooltip("Comma separated name prefixes that turn an object into a separator.")]
        public string separatorPrefixes = "---,===,//";
        public SeparatorAlign separatorAlign = SeparatorAlign.Center;
        public bool separatorOutline = true;
        public bool separatorsEditorOnly = true;

        [Header("Navigation Bar")]
        [Tooltip("Bookmarks for quick access. Alt + Click a Hierarchy row to add or remove one.")]
        public bool navBar = true;
        [Tooltip("Bar: inside the new Hierarchy, under its search field. Tab: a separate Bookmarks window you can dock anywhere (always used with the classic Hierarchy).")]
        public NavBarMode navBarMode = NavBarMode.Bar;
        [Tooltip("Alt+1..9 jumps to bookmark 1..9 (remap in Edit > Shortcuts > Empathy Hierarchy).")]
        public bool navNumberShortcuts = true;
        [Tooltip("Small ★ on Hierarchy rows that are in the navigation bar.")]
        public bool navStarOnRows = true;
        [Tooltip("Height of the bookmark chips; the text follows (both Hierarchies, Bar and Tab).")]
        [Range(20f, 28f)] public float bookmarkChipSize = 22f;

        [Header("Tag & Layer")]
        [Tooltip("Combined: one TAG/LAY chip. Circles: T and L dots. Click either to open the picker.")]
        public TagLayerStyle tagLayerStyle = TagLayerStyle.Circles;
        [Tooltip("Show the chip even for Untagged / Default (drawn quietly).")]
        public bool showDefaultTagLayer = true;
        [Range(40f, 200f)] public float tagLayerMaxWidth = 120f;
        [Tooltip("Fixed colors per tag name. Tags not listed get an automatic color.")]
        public List<NamedColor> tagColors = new List<NamedColor>();
        [Tooltip("Fixed colors per layer name. Layers not listed get an automatic color.")]
        public List<NamedColor> layerColors = new List<NamedColor>();

        [Header("Colors")]
        [Tooltip("Saturation for every styled object that follows the global value (set from Edit Style > SATURATION).")]
        [Range(0f, 1f)] public float globalSaturation = 1f;
        [Tooltip("Brightness for objects that follow the global value (0 = black).")]
        [Range(0f, 1f)] public float globalBrightness = 1f;
        [Tooltip("Opacity for objects that follow the global value (0 = fades into the Hierarchy background).")]
        [Range(0f, 1f)] public float globalOpacity = 1f;

        [Header("Icons")]
        [Tooltip("Icons you added with + in the Edit Style window (asset GUIDs). Shown first in the All tab.")]
        public List<string> customIcons = new List<string>();
        [Tooltip("Built-in icons hidden from the All tab (right-click an icon > Hide).")]
        public List<string> hiddenIcons = new List<string>();
        [Tooltip("Icons you added to the Recommended section (by name).")]
        public List<string> recommendedAdded = new List<string>();
        [Tooltip("Built-in recommended icons you removed (by name).")]
        public List<string> recommendedRemoved = new List<string>();
        [Tooltip("▾ next to scene headers in the Hierarchy opens a scene list (also Alt + Shift + S).")]
        public bool sceneSelector = true;
        [HideInInspector] public List<string> favoriteScenes = new List<string>(); // scene GUIDs, in the order shown

        [Header("Right Side Widgets")]
        public bool childCountBadge = true;
        [Tooltip("Direct: children only. Total: all descendants. Both: \"2 · 15\". Smart: total while folded, direct while unfolded (classic Hierarchy: shows Total).")]
        public ChildCountMode childCountMode = ChildCountMode.Smart;
        [Tooltip("Subtle: faint overlay. Accent: uses the row/branch color (falls back to Badge Color). Custom: always Badge Color.")]
        public WidgetColorMode badgeMode = WidgetColorMode.Accent;
        public Color badgeColor = new Color(0.36f, 0.43f, 0.58f, 1f);
        public bool activeToggle = true;
        public ActiveToggleStyle activeToggleStyle = ActiveToggleStyle.Checkbox;
        [Tooltip("Subtle: neutral gray. Accent: uses the row/branch color (falls back to Toggle On Color). Custom: always Toggle On Color.")]
        public WidgetColorMode activeToggleColorMode = WidgetColorMode.Accent;
        public Color toggleOnColor = new Color(0.24f, 0.73f, 0.42f, 1f);

        [NonSerialized] string[] _prefixCache;
        [NonSerialized] string _prefixSource;

        /// <summary>Which Hierarchy ("new" / "legacy") the mode-dependent defaults were last set for.</summary>
        [HideInInspector] public string defaultsMode = "";

        [NonSerialized] NavBarMode _savedNavMode;
        [NonSerialized] bool _savedNavBar;

        void OnEnable()
        {
            hideFlags &= ~HideFlags.NotEditable;
            _savedNavMode = navBarMode;
            _savedNavBar = navBar;
        }

        static bool? _newHierarchyActive;

        /// <summary>True while rows are drawn by the new (UI Toolkit) Hierarchy; learnt from whichever renderer draws first.</summary>
        internal static bool NewHierarchyActive
        {
            get
            {
#if UNITY_6000_6_OR_NEWER
                return _newHierarchyActive ?? true;
#else
                return _newHierarchyActive ?? false;
#endif
            }
        }

        /// <summary>Called by both renderers; cheap after the first call. Switches the mode-dependent defaults when the Hierarchy type changes.</summary>
        internal static void NoteHierarchy(bool isNew)
        {
            if (_newHierarchyActive == isNew) return;
            _newHierarchyActive = isNew;
            instance.ApplyModeDefaults(isNew);
            EditorApplication.delayCall += () => EHBookmarkWindow.Sync(EHBookmarkWindow.FirstTimeForProject);
#if UNITY_6000_5_OR_NEWER
            if (!isNew) EHNewHierarchyPrompt.MaybeShow();
#endif
        }

        // bump when the per-Hierarchy defaults change, so projects pick up the new set once
        const string NewDefaultsVersion = "3", LegacyDefaultsVersion = "3";

        void ApplyModeDefaults(bool isNew)
        {
            var mode = isNew ? "new:" + NewDefaultsVersion : "legacy:" + LegacyDefaultsVersion;
            if (defaultsMode == "new") defaultsMode = "new:2";
            if (isNew && defaultsMode == "new:2")
            {
                // new v3: only these two change (the rest of an existing setup stays as it is)
                defaultsMode = mode;
                tagLayerStyle = TagLayerStyle.Circles;
                activeToggleStyle = ActiveToggleStyle.Checkbox;
                EditorApplication.delayCall += () => { SaveSettings(); EditorApplication.RepaintHierarchyWindow(); };
                return;
            }
            if (defaultsMode == mode) return;
            var first = string.IsNullOrEmpty(defaultsMode);
            defaultsMode = mode;
            // keep existing new-Hierarchy setups untouched; otherwise use what suits this Hierarchy
            if (!(first && isNew))
            {
                extendRootRows = isNew;      // only the new Hierarchy can widen root rows
                accentBar = isNew;           // the classic arrow area hides most of it
                childCountMode = isNew ? ChildCountMode.Smart : ChildCountMode.Total; // classic can't tell folded rows
                componentAwareIcons = !isNew; // classic shows plain cube icons, so component icons help more there
                if (!isNew) outlineFullWidth = false; // cleaner next to the classic arrows
                // T / L circles and a plain checkbox everywhere; the count badge only in the new Hierarchy
                childCountBadge = isNew;
                tagLayerStyle = TagLayerStyle.Circles;
                activeToggleStyle = ActiveToggleStyle.Checkbox;
            }
            EditorApplication.delayCall += () => { SaveSettings(); EditorApplication.RepaintHierarchyWindow(); };
        }

        /// <summary>Options that only work in the new Hierarchy; hidden from the settings page while the classic one is used.</summary>
        internal static bool NewHierarchyOnly(string field)
        {
            if (NewHierarchyActive) return false;
            return field == nameof(extendRootRows) || field == nameof(arrowTint) || field == nameof(navBarMode) ||
                   field == nameof(navStarOnRows);
        }

        public string[] SeparatorPrefixList
        {
            get
            {
                if (_prefixCache == null || _prefixSource != separatorPrefixes)
                {
                    _prefixSource = separatorPrefixes;
                    var parts = (separatorPrefixes ?? "").Split(',');
                    var list = new System.Collections.Generic.List<string>();
                    foreach (var p in parts)
                    {
                        var s = p.Trim();
                        if (s.Length > 0) list.Add(s);
                    }
                    _prefixCache = list.ToArray();
                }
                return _prefixCache;
            }
        }

        public void SaveSettings()
        {
            System.IO.Directory.CreateDirectory("ProjectSettings/EmpathyHierarchy");
            Save(true);
            // switching to Tab (or turning bookmarks back on) opens the window; Bar / off closes it
            bool open = navBar && (navBarMode != _savedNavMode || navBar != _savedNavBar);
            _savedNavMode = navBarMode;
            _savedNavBar = navBar;
            EHBookmarkWindow.Sync(open);
        }
    }

    static class EHSettingsProvider
    {
        static SerializedObject _so;
        static Vector2 _scroll;

        [SettingsProvider]
        static SettingsProvider Create()
        {
            return new SettingsProvider("Project/Empathy Hierarchy", SettingsScope.Project)
            {
                label = "Empathy Hierarchy",
                guiHandler = _ =>
                {
                    var settings = EHSettings.instance;
                    if (_so == null || _so.targetObject != settings) _so = new SerializedObject(settings);
                    _so.Update();

                    EditorGUI.BeginChangeCheck();
                    _scroll = EditorGUILayout.BeginScrollView(_scroll);
                    var it = _so.GetIterator();
                    bool enter = true;
                    while (it.NextVisible(enter))
                    {
                        enter = false;
                        if (it.name == "m_Script" || EHSettings.NewHierarchyOnly(it.name)) continue;
                        EditorGUILayout.PropertyField(it, true);
                    }
                    EditorGUILayout.Space(10);
                    EditorGUILayout.HelpBox(
                        "Edit an object's style: select it and press Alt+E (remap in Edit > Shortcuts > Empathy Hierarchy), " +
                        "or right-click > Empathy Hierarchy.", MessageType.Info);
                    EditorGUILayout.EndScrollView();

                    if (EditorGUI.EndChangeCheck())
                    {
                        _so.ApplyModifiedProperties();
                        settings.SaveSettings();
                        EditorApplication.RepaintHierarchyWindow();
                        EHStore.NotifyChanged();
                        EHNav.QueueRefresh(); // bookmark chips (size, ★) rebuild
                    }
                },
                keywords = new[] { "hierarchy", "tree", "stripe", "separator", "color", "empathy" }
            };
        }
    }
}
