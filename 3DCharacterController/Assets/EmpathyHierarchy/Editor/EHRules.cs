using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EmpathyHierarchy
{
    public enum NameMatch { Word, Contains, StartsWith, EndsWith } // only Contains is used now
    public enum RuleGroup { Custom, Component, Naming }
    /// <summary>What happens to a match that sits inside an already matched parent (one setting for all rules).</summary>
    public enum NestedMode { TopMatchOnly, MatchAll, IconOnly, Tinted }

    /// <summary>
    /// "Objects whose name contains … or that have component … look like this."
    /// Every rule works the same way; the built-in naming set just fills in names, the component set components.
    /// </summary>
    [Serializable]
    public class StyleRule
    {
        public string id;
        public string name = "New Rule";
        public bool enabled = true;
        public RuleGroup group = RuleGroup.Custom;

        [Tooltip("Comma separated. Matches when the object's name contains one of them (case doesn't matter).")]
        public string names = "";
        public NameMatch nameMatch = NameMatch.Contains; // kept for old files; names always match by "contains" now
        [Tooltip("Comma separated component type names, e.g. Light, AudioSource. End with * to match a prefix (Cinemachine*).")]
        public string components = "";

        /// <summary>Palette slot the color came from (-1 = picked freely); lets the rules follow a palette change.</summary>
        public int paletteSlot = -1;
        /// <summary>Same for the gradient's second color (-2 = not known yet: older files, worked out on a theme change).</summary>
        public int paletteSlot2 = -2;
        public ObjectStyle style = new ObjectStyle();

        // parsed conditions (rebuilt when the text changes)
        [NonSerialized] string _namesParsedFrom, _componentsParsedFrom;
        [NonSerialized] string[] _names = Array.Empty<string>();
        [NonSerialized] string[] _components = Array.Empty<string>();
        [NonSerialized] ObjectStyle _iconOnly, _tinted;

        public StyleRule Clone()
        {
            var r = (StyleRule)MemberwiseClone();
            r.style = style.Clone();
            r._namesParsedFrom = r._componentsParsedFrom = null;
            r._iconOnly = r._tinted = null;
            return r;
        }

        public bool HasCondition => Names.Length > 0 || Components.Length > 0;

        /// <summary>The rule's look without color (a match inside a matched parent, "icon only").</summary>
        public ObjectStyle IconOnlyStyle
        {
            get
            {
                if (_iconOnly != null) return _iconOnly;
                _iconOnly = style.Clone();
                _iconOnly.hasColor = false;
                _iconOnly.bold = false;
                _iconOnly.tintChildren = false;
                if (_iconOnly.iconMode == IconMode.Default) _iconOnly.iconMode = IconMode.Auto; // something to show
                return _iconOnly;
            }
        }

        string[] Names
        {
            get
            {
                if (!ReferenceEquals(_namesParsedFrom, names))
                {
                    _namesParsedFrom = names;
                    _names = Split(names, false);
                }
                return _names;
            }
        }

        string[] Components
        {
            get
            {
                if (!ReferenceEquals(_componentsParsedFrom, components))
                {
                    _componentsParsedFrom = components;
                    _components = Split(components, false);
                }
                return _components;
            }
        }

        internal void ResetParse()
        {
            _namesParsedFrom = _componentsParsedFrom = null;
            _iconOnly = _tinted = null;
        }

        /// <summary>The rule's own color as a light child tint, with its icon (a match inside a matched parent, "tint").</summary>
        public ObjectStyle TintedStyle
        {
            get
            {
                if (_tinted != null) return _tinted;
                _tinted = style.Clone();
                _tinted.tintOnly = true; // keeps its bold: an own-tint match still reads as a match
                if (_tinted.iconMode == IconMode.Default) _tinted.iconMode = IconMode.Auto;
                return _tinted;
            }
        }

        static string[] Split(string s, bool normalize)
        {
            if (string.IsNullOrWhiteSpace(s)) return Array.Empty<string>();
            var parts = s.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            var list = new List<string>(parts.Length);
            foreach (var p in parts)
            {
                var t = p.Trim();
                if (t.Length > 0) list.Add(t);
            }
            return list.ToArray();
        }

        /// <summary>Short text for lists: "name is Lighting, Lights · has Light".</summary>
        public string Summary
        {
            get
            {
                string n = string.IsNullOrWhiteSpace(names) ? null : "name contains " + names.Trim();
                string c = string.IsNullOrWhiteSpace(components) ? null : "has " + components.Trim();
                if (n != null && c != null) return n + "  or  " + c;
                return n ?? c ?? "no condition yet";
            }
        }

        /// <summary>Name or component matches (either one is enough).</summary>
        internal bool Matches(GameObject go, List<Component> comps) => Hit(go, comps, out _, out _);

        internal bool Hit(GameObject go, List<Component> comps, out bool byName, out bool byComponent)
        {
            byName = byComponent = false;
            if (!enabled || !HasCondition) return false;
            byName = Names.Length > 0 && NameMatches(go.name);
            byComponent = Components.Length > 0 && ComponentMatches(comps);
            return byName || byComponent;
        }

        internal bool NeedsComponents => Components.Length > 0;

        bool NameMatches(string raw)
        {
            foreach (var n in Names)
                if (raw.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        bool ComponentMatches(List<Component> comps)
        {
            foreach (var want in Components)
            {
                bool prefix = want.EndsWith("*");
                var w = prefix ? want.Substring(0, want.Length - 1) : want;
                foreach (var c in comps)
                {
                    if (c == null) continue;
                    var tn = c.GetType().Name;
                    if (prefix ? tn.StartsWith(w, StringComparison.OrdinalIgnoreCase) : string.Equals(tn, w, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Automatic styles. Your own style on an object (Alt+E) always wins; then custom rules, then the
    /// component and naming sets in the chosen order. Inside a list the first matching rule wins.
    /// Shared with the team through ProjectSettings.
    /// </summary>
    [FilePath("ProjectSettings/EmpathyHierarchy/Rules.asset", FilePathAttribute.Location.ProjectFolder)]
    public class EHRules : ScriptableSingleton<EHRules>
    {
        /// <summary>Checks the saved file (backup, script link) before it is read.</summary>
        public new static EHRules instance
        {
            get { EHDataGuard.Ensure(); return ScriptableSingleton<EHRules>.instance; }
        }

        public bool seeded;
        public int presetVersion;
        /// <summary>The set in use, one at a time: Naming base, Component base or Custom (your own rules only).</summary>
        public RuleGroup activeSet = RuleGroup.Naming;
        /// <summary>Fill used by the built-in sets / by custom rules (set from the Rules window; new rules start with it).</summary>
        public FillMode builtInFill = FillMode.Solid;
        public FillMode customFill = FillMode.Soft;

        public FillMode FillFor(RuleGroup g) => g == RuleGroup.Custom ? customFill : builtInFill;
        /// <summary>Matches inside an already matched parent: skipped, full look, or icon only.</summary>
        public NestedMode nested = NestedMode.Tinted;
        public List<StyleRule> custom = new List<StyleRule>();
        public List<StyleRule> component = new List<StyleRule>();
        public List<StyleRule> naming = new List<StyleRule>();

        internal static readonly char[] Symbols = " -=/_*#~.|[](){}<>:".ToCharArray();

        public static event Action Changed;

        // ---- cache: object → winning rule (null = none), cleared on any hierarchy / rule change
        static readonly Dictionary<GameObject, StyleRule> RawCache = new Dictionary<GameObject, StyleRule>(); // ignoring parents
        static readonly List<Component> Comps = new List<Component>();

        [InitializeOnLoadMethod]
        static void Hook()
        {
            EditorApplication.hierarchyChanged += ClearCache;
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += (s, m) => ClearCache();
            Undo.undoRedoPerformed += ClearCache;
            Undo.postprocessModifications += OnModifications;
        }

        static bool _renameQueued;

        /// <summary>A rename can change which rule applies: re-check and redraw (rows don't always rebind on rename).</summary>
        static UndoPropertyModification[] OnModifications(UndoPropertyModification[] mods)
        {
            foreach (var m in mods)
            {
                var pm = m.currentValue;
                if (pm == null || !(pm.target is GameObject) || pm.propertyPath != "m_Name") continue;
                ClearCache();
                if (!_renameQueued)
                {
                    _renameQueued = true;
                    EditorApplication.delayCall += () => { _renameQueued = false; ClearCache(); EHStore.NotifyChanged(); };
                }
                break;
            }
            return mods;
        }

        static void ClearCache()
        {
            Hits.Clear();
            RawCache.Clear();
        }

        void OnEnable()
        {
            hideFlags &= ~HideFlags.NotEditable;
            if (!seeded || presetVersion < 2)
            {
                // first run, or the built-in sets changed shape (names now match by "contains")
                seeded = true;
                presetVersion = EHRulePresets.Version;
                component = EHRulePresets.ComponentSet();
                naming = EHRulePresets.NamingSet();
                EditorApplication.delayCall += SaveNow;
            }
            else if (presetVersion < 3)
            {
                // v3: coloring the children is on by default (kept the rest of your edits)
                presetVersion = 3;
                foreach (var r in component) r.style.tintChildren = true;
                foreach (var r in naming) r.style.tintChildren = true;
                EditorApplication.delayCall += SaveNow;
            }
            if (presetVersion < 4)
            {
                presetVersion = 4;
                nested = NestedMode.Tinted; // v4: "match all · own tint" is the default
                EditorApplication.delayCall += SaveNow;
            }
            if (presetVersion < 5)
            {
                // v5: rule names are bold by default (the checkbox on each rule row)
                presetVersion = 5;
                foreach (var r in All()) r.style.bold = true;
                EditorApplication.delayCall += SaveNow;
            }
            if (presetVersion < 6)
            {
                // v6: new built-in component set (solid, two colors, custom icons). Only replaces it when it is
                // still the old untouched one (every rule on the automatic icon); edited sets are kept.
                presetVersion = 6;
                bool untouched = component.Count > 0;
                foreach (var r in component) if (r.style == null || r.style.iconMode != IconMode.Auto) { untouched = false; break; }
                if (untouched) component = EHRulePresets.ComponentSet();
                EditorApplication.delayCall += SaveNow;
            }
        }

        public void SaveNow()
        {
            System.IO.Directory.CreateDirectory("ProjectSettings/EmpathyHierarchy");
            Save(true);
        }

        /// <summary>Call after editing rules: saves, clears the cache and repaints.</summary>
        public static void MarkChanged()
        {
            foreach (var r in instance.All()) r.ResetParse();
            ClearCache();
            instance.SaveNow();
            Changed?.Invoke();
            EHStore.NotifyChanged();
            EditorApplication.RepaintHierarchyWindow();
        }

        /// <summary>Rules in priority order.</summary>
        public IEnumerable<StyleRule> Ordered()
        {
            foreach (var r in ListOf(activeSet)) yield return r;
        }

        public IEnumerable<StyleRule> All()
        {
            foreach (var r in custom) yield return r;
            foreach (var r in component) yield return r;
            foreach (var r in naming) yield return r;
        }

        public List<StyleRule> ListOf(RuleGroup g) => g == RuleGroup.Custom ? custom : g == RuleGroup.Component ? component : naming;

        /// <summary>The rule that styles <paramref name="go"/>, or null. Cached until something changes.</summary>
        public static StyleRule Match(GameObject go) => Get(go).rule;

        /// <summary>The look rules give <paramref name="go"/> (full, or icon only inside a matched parent), or null.</summary>
        public static ObjectStyle StyleFor(GameObject go)
        {
            var h = Get(go);
            if (h.rule == null) return null;
            return h.look == NestedMode.IconOnly ? h.rule.IconOnlyStyle : h.look == NestedMode.Tinted ? h.rule.TintedStyle : h.rule.style;
        }

        struct Hit { public StyleRule rule; public NestedMode look; } // look: MatchAll = full, IconOnly, Tinted
        static readonly Dictionary<GameObject, Hit> Hits = new Dictionary<GameObject, Hit>();

        static Hit Get(GameObject go)
        {
            if (go == null) return default;
            if (Hits.TryGetValue(go, out var h)) return h;
            h = Evaluate(go);
            Hits[go] = h;
            return h;
        }

        /// <summary>
        /// First rule that matches wins. Inside a parent that some rule matches, the Rules window's setting decides:
        /// skip it (top match only), full look (match all), just the icon (icon only) or its own color as a
        /// light child tint with its icon (tint: it's not a top-level object, so it reads as part of the group).
        /// </summary>
        static Hit Evaluate(GameObject go)
        {
            bool compsLoaded = false;
            int insideMatch = -1; // -1 unknown, 0 no, 1 yes
            foreach (var r in instance.Ordered())
            {
                if (!r.enabled || !r.HasCondition) continue;
                if (!compsLoaded && r.NeedsComponents) { go.GetComponents(Comps); compsLoaded = true; }
                if (!r.Hit(go, Comps, out _, out _)) continue;
                var mode = instance.nested;
                if (mode == NestedMode.MatchAll) { Comps.Clear(); return new Hit { rule = r, look = NestedMode.MatchAll }; }
                if (insideMatch < 0) insideMatch = ParentMatched(go) ? 1 : 0;
                Comps.Clear();
                if (insideMatch == 0) return new Hit { rule = r, look = NestedMode.MatchAll };
                return mode == NestedMode.TopMatchOnly ? default : new Hit { rule = r, look = mode };
            }
            Comps.Clear();
            return default;
        }

        static bool ParentMatched(GameObject go)
        {
            for (var t = go.transform.parent; t != null; t = t.parent)
                if (Raw(t.gameObject)) return true;
            return false;
        }

        static readonly List<Component> RawComps = new List<Component>();

        /// <summary>Does any rule match this object on its own (ignoring parents)? Cached.</summary>
        static bool Raw(GameObject go)
        {
            if (RawCache.TryGetValue(go, out var hit)) return hit != null;
            hit = null;
            bool loaded = false;
            foreach (var r in instance.Ordered())
            {
                if (!r.enabled || !r.HasCondition) continue;
                if (!loaded && r.NeedsComponents) { go.GetComponents(RawComps); loaded = true; }
                if (r.Matches(go, RawComps)) { hit = r; break; }
            }
            RawComps.Clear();
            RawCache[go] = hit;
            return hit != null;
        }

        /// <summary>True if <paramref name="rule"/> alone would match (ignores priority); used for match counts.</summary>
        public static bool MatchesAlone(StyleRule rule, GameObject go)
        {
            go.GetComponents(Comps);
            bool was = rule.enabled;
            rule.enabled = true;
            bool m = rule.Matches(go, Comps);
            rule.enabled = was;
            Comps.Clear();
            return m;
        }

        /// <summary>Saves nothing: for live preview while a rule is edited (MarkChanged saves).</summary>
        public static void MarkChangedLive()
        {
            foreach (var r in instance.All()) r.ResetParse();
            ClearCache();
            Changed?.Invoke();
            EHStore.NotifyChanged();
            EditorApplication.RepaintHierarchyWindow();
        }

        static readonly Stack<Transform> Walk = new Stack<Transform>();

        /// <summary>Objects in the open scenes that <paramref name="rule"/> matches on its own (up to <paramref name="max"/>).</summary>
        public static int Collect(StyleRule rule, List<GameObject> into, int max = int.MaxValue)
        {
            int count = 0;
            Walk.Clear();
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects()) Walk.Push(root.transform);
            }
            while (Walk.Count > 0)
            {
                var t = Walk.Pop();
                if (MatchesAlone(rule, t.gameObject))
                {
                    count++;
                    into?.Add(t.gameObject);
                    if (count >= max) break;
                }
                for (int c = 0; c < t.childCount; c++) Walk.Push(t.GetChild(c));
            }
            Walk.Clear();
            return count;
        }

        /// <summary>How many objects in the open scenes each rule matches on its own (one pass over the scenes).</summary>
        public static void CountAll(Dictionary<StyleRule, int> counts)
        {
            counts.Clear();
            var rules = new List<StyleRule>(instance.All());
            foreach (var r in rules) counts[r] = 0;
            Walk.Clear();
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects()) Walk.Push(root.transform);
            }
            while (Walk.Count > 0)
            {
                var t = Walk.Pop();
                var go = t.gameObject;
                go.GetComponents(Comps);
                foreach (var r in rules)
                {
                    if (!r.HasCondition) continue;
                    bool was = r.enabled;
                    r.enabled = true; // count even switched-off rules
                    if (r.Matches(go, Comps)) counts[r]++;
                    r.enabled = was;
                }
                Comps.Clear();
                for (int c = 0; c < t.childCount; c++) Walk.Push(t.GetChild(c));
            }
        }

        // components that say what an object is (used by "Make Rule"); meshes, colliders etc. are too common
        static readonly HashSet<string> Notable = new HashSet<string>
        {
            "Camera", "Light", "ReflectionProbe", "LightProbeGroup", "Volume", "AudioSource", "AudioReverbZone", "Canvas",
            "EventSystem", "ParticleSystem", "VisualEffect", "PlayableDirector", "Animator", "CharacterController",
            "NavMeshAgent", "NavMeshSurface", "NavMeshObstacle", "Terrain", "WindZone"
        };

        /// <summary>A new custom rule that gives objects like <paramref name="go"/> the look <paramref name="look"/>.</summary>
        public static StyleRule NewRuleFor(GameObject go, ObjectStyle look)
        {
            var rule = new StyleRule { id = Guid.NewGuid().ToString("N"), group = RuleGroup.Custom, style = look.Clone() };
            rule.style.id = null;
            string comp = null;
            go.GetComponents(Comps);
            foreach (var c in Comps)
            {
                if (c == null) continue;
                var n = c.GetType().Name;
                if (Notable.Contains(n)) { comp = n; break; }
                if (comp == null && c is MonoBehaviour && !(c.GetType().Namespace ?? "").StartsWith("UnityEngine")) comp = n; // own script
            }
            Comps.Clear();
            if (comp != null)
            {
                rule.components = comp;
                rule.name = comp;
            }
            else
            {
                var trimmed = go.name.Trim(Symbols);
                rule.names = trimmed;
                rule.name = trimmed;
            }
            return rule;
        }

        /// <summary>Recolors the built-in sets (rules whose color came from a palette slot) with the current palette.</summary>
        public static void RecolorFromPalette() => RecolorFromPalette(false);

        /// <summary>Recolors palette-based rules: the built-in sets, or the custom rules.</summary>
        public static void RecolorFromPalette(bool custom)
        {
            foreach (var r in instance.All())
            {
                if ((r.group == RuleGroup.Custom) != custom) continue;
                if (r.paletteSlot >= 0)
                {
                    r.style.color = EHStyleWindow.PaletteColor(r.paletteSlot);
                    r.style.hasColor = true;
                }
                // the gradient's end color follows the theme too
                if (r.paletteSlot2 == -2) r.paletteSlot2 = EHStyleWindow.AnyPaletteSlotOf(r.style.color2);
                if (r.paletteSlot2 >= 0) r.style.color2 = EHStyleWindow.PaletteColor(r.paletteSlot2);
            }
            MarkChanged();
        }

        /// <summary>Gives every built-in rule the same fill (Soft / Solid / Outline).</summary>
        public static void SetFill(bool custom, FillMode fill)
        {
            if (custom) instance.customFill = fill; else instance.builtInFill = fill;
            foreach (var r in instance.All())
                if ((r.group == RuleGroup.Custom) == custom) r.style.fill = fill;
            MarkChanged();
        }

        /// <summary>Puts a preset list back to the built-in set (current palette).</summary>
        public static void ResetGroup(RuleGroup g)
        {
            if (g == RuleGroup.Component) instance.component = EHRulePresets.ComponentSet();
            else if (g == RuleGroup.Naming) instance.naming = EHRulePresets.NamingSet();
            foreach (var r in instance.ListOf(g))
            {
                r.style.fill = instance.builtInFill;
                // the built-in colors are Classic: follow the theme in use right away
                if (EHStyleWindow.Palette == 0) continue;
                if (r.paletteSlot >= 0) { r.style.color = EHStyleWindow.PaletteColor(r.paletteSlot); r.style.hasColor = true; }
                if (r.paletteSlot2 == -2) r.paletteSlot2 = EHStyleWindow.AnyPaletteSlotOf(r.style.color2);
                if (r.paletteSlot2 >= 0) r.style.color2 = EHStyleWindow.PaletteColor(r.paletteSlot2);
            }
            MarkChanged();
        }
    }

    /// <summary>The built-in rule sets.</summary>
    static class EHRulePresets
    {
        // palette slots: 0 blue, 1 green, 2 yellow, 3 orange, 4 red, 5 purple, 6 gray
        const int Blue = 0, Green = 1, Yellow = 2, Orange = 3, Red = 4, Purple = 5, Gray = 6;
        /// <summary>Bump when the sets change; projects then get the new sets once.</summary>
        public const int Version = 6;

        static StyleRule Make(RuleGroup g, string name, string names, NameMatch match, string components, int slot, bool tintChildren, bool autoIcon)
        {
            var st = new ObjectStyle
            {
                hasColor = true,
                color = EHStyleWindow.PaletteColor(slot),
                fill = FillMode.Soft,
                tintChildren = tintChildren,
                bold = true,
                iconMode = autoIcon ? IconMode.Auto : IconMode.Default
            };
            return new StyleRule
            {
                id = Guid.NewGuid().ToString("N"),
                group = g, name = name, names = names, nameMatch = match, components = components,
                paletteSlot = slot, style = st
            };
        }

        /// <summary>
        /// By component: objects that carry these components. Colors, gradients and icons as set up by the author
        /// (solid fill, two-color gradient, custom icons).
        /// </summary>
        public static List<StyleRule> ComponentSet() => new List<StyleRule>
        {
            Comp("Cameras", "Camera, Cinemachine*", Purple, new Color(0.6078f, 0.4196f, 0.9098f), new Color(0.2392f, 0.7294f, 0.4196f), "res:icons/processed/unityengine/d_camera icon.asset"),
            Comp("Lights", "Light, ReflectionProbe, LightProbeGroup", Gray, new Color(0.549f, 0.5765f, 0.6157f), new Color(0.898f, 0.7255f, 0.2078f), "res:icons/processed/unityengine/d_light icon.asset"),
            Comp("Post / Volume", "Volume, PostProcessVolume", Blue, new Color(0.2275f, 0.5608f, 0.8471f), new Color(0.608f, 0.42f, 0.91f), "res:icons/d_pretexturergb.png"),
            Comp("UI", "Canvas, EventSystem", Orange, new Color(0.9216f, 0.5098f, 0.1961f), new Color(0.2392f, 0.7294f, 0.4196f), "res:icons/processed/unityengine/guilayer icon.asset"),
            Comp("Audio", "AudioSource, AudioReverbZone", Purple, new Color(0.6078f, 0.4196f, 0.9098f), new Color(0.898f, 0.7255f, 0.2078f), "res:icons/processed/unityengine/d_audiosource icon.asset"),
            Comp("VFX", "ParticleSystem, VisualEffect", Red, new Color(0.898f, 0.2824f, 0.302f), new Color(0.898f, 0.7255f, 0.2078f), "res:icons/processed/unityengine/vfx/d_visualeffectasset icon.asset"),
            Comp("Timeline", "PlayableDirector", Purple, new Color(0.6078f, 0.4196f, 0.9098f), new Color(0.2275f, 0.5608f, 0.8471f), "res:icons/d_unityeditor.timeline.timelinewindow.png"),
            Comp("Characters", "Animator, CharacterController", Red, new Color(0.898f, 0.2824f, 0.302f), new Color(0.2275f, 0.5608f, 0.8471f), "res:icons/processed/unityengine/d_avatar icon.asset"),
            Comp("AI / Navigation", "NavMeshAgent, NavMeshSurface, NavMeshObstacle", Gray, new Color(0.549f, 0.5765f, 0.6157f), new Color(0.898f, 0.2824f, 0.302f), "res:icons/processed/unityengine/ai/d_navmeshdata icon.asset"),
            Comp("Environment", "Terrain, WindZone", Blue, new Color(0.2275f, 0.5608f, 0.8471f), new Color(0.2392f, 0.7294f, 0.4196f), "res:icons/d_tree_icon_leaf.png"),
        };

        static StyleRule Comp(string name, string components, int slot, Color color, Color color2, string icon)
        {
            var r = Make(RuleGroup.Component, name, "", NameMatch.Contains, components, slot, true, false);
            r.style.color = color;
            r.style.fill = FillMode.Solid;
            r.style.gradient = GradientMode.TwoColor;
            r.style.color2 = color2;
            r.style.iconMode = IconMode.Custom;
            r.style.icon = icon;
            r.style.iconDarken = 0f;
            return r;
        }

        /// <summary>
        /// By name ("contains", case ignored): the group objects that usually sit at the top of a scene.
        /// Colors, gradients and icons as set up by the author (Classic palette, solid fill, dark icons).
        /// Very short words (UI, AI, FX…) are left out: they hide inside many other names (Build, Chair, …).
        /// </summary>
        public static List<StyleRule> NamingSet() => new List<StyleRule>
        {
            Named("Managers & Systems", "Manager, System, Controller, Service, Bootstrap, Network", 4, new Color(0.898f, 0.2824f, 0.302f), (GradientMode)2, new Color(0.9216f, 0.5098f, 0.1961f), "res:icons/processed/d_settings icon.asset", 1.0f),
            Named("Cameras", "Camera, Cinemachine", 0, new Color(0.2275f, 0.5608f, 0.8471f), (GradientMode)1, new Color(0.2392f, 0.7294f, 0.4196f), "res:icons/processed/unityengine/d_camera icon.asset", 1.0f),
            Named("Lighting", "Light, Probe, PostProcess, Volume, Sky", 2, new Color(0.898f, 0.7255f, 0.2078f), (GradientMode)2, new Color(0.9216f, 0.5098f, 0.1961f), "res:icons/processed/unityengine/d_light icon.asset", 1.0f),
            Named("Environment / Level", "Environment, Level, World, Stage, Terrain, Props, Geometry, Scenery, Background, Foliage", 1, new Color(0.2392f, 0.7294f, 0.4196f), (GradientMode)2, new Color(0.2275f, 0.5608f, 0.8471f), "res:icons/d_tree_icon.png", 1.0f),
            Named("Gameplay", "Gameplay, Player, Character, Enemy, NPC, Interactable, Pickup, Weapon, Vehicle", 3, new Color(0.9216f, 0.5098f, 0.1961f), (GradientMode)2, new Color(0.608f, 0.42f, 0.91f), "res:icons/d_unityeditor.gameview.png", 1.0f),
            Named("Spawns & Markers", "Spawn, Waypoint, Checkpoint, Trigger, Marker, NavMesh", 0, new Color(0.2275f, 0.5608f, 0.8471f), (GradientMode)2, new Color(0.898f, 0.7255f, 0.2078f), "res:icons/packagemanager/light/download-available-selected-focused.png", 1.0f),
            Named("UI", "HUD, Canvas, Menu, Interface, Popup", 5, new Color(0.6078f, 0.4196f, 0.9098f), (GradientMode)2, new Color(0.9216f, 0.5098f, 0.1961f), "res:icons/d_leftpanel.png", 1.0f),
            Named("Audio", "Audio, Sound, Music, SFX, BGM, Ambience", 5, new Color(0.6078f, 0.4196f, 0.9098f), (GradientMode)2, new Color(0.2275f, 0.5608f, 0.8471f), "res:icons/processed/unityengine/d_audiosource icon.asset", 1.0f),
            Named("VFX", "VFX, Effect, Particle", 4, new Color(0.898f, 0.2824f, 0.302f), (GradientMode)2, new Color(0.898f, 0.7255f, 0.2078f), "res:icons/processed/unityengine/vfx/d_visualeffectasset icon.asset", 0.981f),
            Named("Timeline / Cutscene", "Timeline, Cutscene, Cinematic", 5, new Color(0.6078f, 0.4196f, 0.9098f), (GradientMode)2, new Color(0.898f, 0.7255f, 0.2078f), "res:icons/d_unityeditor.timeline.timelinewindow.png", 1.0f),
            Named("Debug / Test", "Debug, Test, Sandbox, WIP", 6, new Color(0.549f, 0.5765f, 0.6157f), (GradientMode)1, new Color(0.608f, 0.42f, 0.91f), "res:icons/toolbars/debug on.png", 1.0f),
        };

        static StyleRule Named(string name, string names, int slot, Color color, GradientMode gradient, Color color2, string icon, float darken)
        {
            var r = Make(RuleGroup.Naming, name, names, NameMatch.Contains, "", slot, true, false);
            r.style.color = color;
            r.style.fill = FillMode.Solid;
            r.style.gradient = gradient;
            r.style.color2 = color2;
            r.style.iconMode = IconMode.Custom;
            r.style.icon = icon;
            r.style.iconDarken = darken;
            return r;
        }
    }
}
