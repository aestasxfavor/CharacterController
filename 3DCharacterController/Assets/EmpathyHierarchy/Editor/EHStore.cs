using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EmpathyHierarchy
{
    [Serializable]
    public class ObjectStyle
    {
        public string id;
        public bool hasColor;
        public Color color = new Color(0.227f, 0.561f, 0.847f, 1f);
        public FillMode fill = FillMode.Solid;
        public GradientMode gradient = GradientMode.None;
        public Color color2 = new Color(0.608f, 0.420f, 0.910f, 1f);
        public bool bold;
        public bool tintChildren = true;
        public IconMode iconMode = IconMode.Default;
        public string icon;
        public SeparatorAlign align = SeparatorAlign.Default;
        [Tooltip("0 = icon as is, 1 = black. Reset whenever another icon is picked.")]
        public float iconDarken;
        /// <summary>Not saved: a rule match inside a matched parent drawn as a light child tint of its own color.</summary>
        [NonSerialized] public bool tintOnly;
        [Tooltip("Follow the project-wide saturation (Edit Style > SATURATION > All objects).")]
        public bool useGlobalSaturation = true;
        public float saturation = 1f;
        public bool useGlobalBrightness = true;
        public float brightness = 1f;
        public bool useGlobalOpacity = true;
        public float opacity = 1f;

        // tone is one project-wide setting (Rules window); the per-object fields above are kept only so old files load
        public float EffectiveSaturation => EHSettings.instance.globalSaturation;
        public float EffectiveBrightness => EHSettings.instance.globalBrightness;
        public float EffectiveOpacity => EHSettings.instance.globalOpacity;

        /// <summary>
        /// A stored (full-strength) color as drawn: saturation, then brightness (toward black),
        /// then opacity (blended into the Hierarchy background, kept opaque so text contrast still works).
        /// </summary>
        public Color Tone(Color c)
        {
            c = EHDraw.Saturate(c, EffectiveSaturation);
            float b = EffectiveBrightness;
            if (b < 0.999f) c = new Color(c.r * b, c.g * b, c.b * b, c.a);
            float o = EffectiveOpacity;
            if (o < 0.999f) c = Color.Lerp(EHDraw.WindowBg, c, Mathf.Clamp01(o));
            c.a = 1f;
            return c;
        }

        /// <summary>Main color as drawn.</summary>
        public Color Color1 => Tone(color);
        /// <summary>Gradient end color as drawn.</summary>
        public Color Color2 => Tone(color2);

        public bool IsEmpty =>
            !hasColor && !bold && iconMode == IconMode.Default && align == SeparatorAlign.Default && iconDarken <= 0f;

        /// <summary>Multiply color for the icon (white = unchanged).</summary>
        public Color IconTint
        {
            get
            {
                float k = 1f - Mathf.Clamp01(iconDarken);
                return new Color(k, k, k, 1f);
            }
        }

        public ObjectStyle Clone() => (ObjectStyle)MemberwiseClone();

        public void CopyVisualsFrom(ObjectStyle o)
        {
            hasColor = o.hasColor; color = o.color; fill = o.fill; gradient = o.gradient;
            color2 = o.color2; bold = o.bold; tintChildren = o.tintChildren;
            iconMode = o.iconMode; icon = o.icon; align = o.align; iconDarken = o.iconDarken;
            useGlobalSaturation = o.useGlobalSaturation; saturation = o.saturation;
            useGlobalBrightness = o.useGlobalBrightness; brightness = o.brightness;
            useGlobalOpacity = o.useGlobalOpacity; opacity = o.opacity;
        }
    }

    [Serializable]
    class SceneStyles
    {
        public string sceneGuid;
        public List<ObjectStyle> objects = new List<ObjectStyle>();
    }

    [Serializable]
    class SceneFile
    {
        public int version = 1;
        public List<ObjectStyle> objects = new List<ObjectStyle>();
    }

    /// <summary>
    /// Holds per-object styles. Nothing is added to scene objects: data lives in
    /// ProjectSettings/EmpathyHierarchy/Scenes/&lt;sceneGuid&gt;.json keyed by GlobalObjectId.
    /// The store itself is a hidden ScriptableObject so edits go through Undo/Redo.
    /// </summary>
    [InitializeOnLoad]
    public class EHStore : ScriptableObject
    {
        const string Folder = "ProjectSettings/EmpathyHierarchy/Scenes";

        [SerializeField] List<SceneStyles> scenes = new List<SceneStyles>();

        static EHStore _instance;
        static readonly Dictionary<GameObject, string> KeyCache = new Dictionary<GameObject, string>();
        static readonly Dictionary<string, ObjectStyle> ById = new Dictionary<string, ObjectStyle>();
        // Objects whose scene was never saved have no stable id yet; kept in memory until saved.
        static readonly Dictionary<GameObject, ObjectStyle> Pending = new Dictionary<GameObject, ObjectStyle>();
        static bool _indexDirty = true;

        // ---- allocation-free lookup caches (cleared on any change)
        static readonly Dictionary<string, string> GuidOfKey = new Dictionary<string, string>();
        static readonly HashSet<string> LoadedGuids = new HashSet<string>();
        struct Branch { public bool found; public Color color; public int distance; }
        static readonly Dictionary<Transform, Branch> BranchSelf = new Dictionary<Transform, Branch>();
        static readonly Dictionary<Transform, Branch> BranchParent = new Dictionary<Transform, Branch>();

        static void ClearBranchCache()
        {
            BranchSelf.Clear();
            BranchParent.Clear();
        }

        /// <summary>Raised whenever styles change (edit, undo/redo, settings) so renderers can refresh.</summary>
        public static event Action Changed;
        public static void NotifyChanged() => Changed?.Invoke();

        static EHStore()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorSceneManager.sceneSaved += OnSceneSaved;
            EditorApplication.hierarchyChanged += ClearBranchCache;
            Changed += ClearBranchCache;
            EditorSceneManager.sceneOpened += (s, m) => { KeyCache.Clear(); };
            EditorSceneManager.sceneClosed += s => { KeyCache.Clear(); };
        }

        static EHStore Instance
        {
            get
            {
                if (_instance == null)
                {
                    var found = Resources.FindObjectsOfTypeAll<EHStore>();
                    _instance = found.Length > 0 ? found[0] : null;
                    if (_instance == null)
                    {
                        _instance = CreateInstance<EHStore>();
                        _instance.hideFlags = HideFlags.HideAndDontSave;
                    }
                    _indexDirty = true;
                    LoadedGuids.Clear();
                    ClearBranchCache();
                }
                return _instance;
            }
        }

        // ------------------------------------------------------------------ lookup

        static bool TryGetKey(GameObject go, out string key, out string sceneGuid)
        {
            key = null; sceneGuid = null;
            if (!KeyCache.TryGetValue(go, out key))
            {
                var gid = GlobalObjectId.GetGlobalObjectIdSlow(go);
                key = (gid.identifierType == 2 && gid.targetObjectId != 0 && !gid.assetGUID.Empty())
                    ? gid.ToString()
                    : string.Empty;
                KeyCache[go] = key;
            }
            if (string.IsNullOrEmpty(key)) return false;
            if (!GuidOfKey.TryGetValue(key, out sceneGuid))
            {
                sceneGuid = SceneGuidFromKey(key);
                GuidOfKey[key] = sceneGuid;
            }
            return sceneGuid != null;
        }

        // GlobalObjectId string format: GlobalObjectId_V1-{type}-{guid}-{fileId}-{prefabId}
        static string SceneGuidFromKey(string key)
        {
            var parts = key.Split('-');
            return parts.Length >= 3 ? parts[2] : null;
        }

        /// <summary>The style a row shows: the object's own style, else the rule that matches it.</summary>
        public static ObjectStyle Get(GameObject go)
        {
            if (go == null) return null;
            var own = GetManual(go);
            if (own != null) return own;
            var rule = EHRules.StyleFor(go);
            if (rule != null) return rule;
            // separators without a style of their own: solid black bar
            return EHHierarchyGUI.IsSeparator(EHCache.Name(go), EHSettings.instance) ? DefaultSeparator : null;
        }

        static readonly ObjectStyle DefaultSeparator = new ObjectStyle
        {
            hasColor = true, color = Color.black, fill = FillMode.Solid, tintChildren = false
        };

        /// <summary>Only the style set on the object itself (Alt+E), ignoring rules.</summary>
        public static ObjectStyle GetManual(GameObject go)
        {
            if (go == null) return null;
            if (Pending.Count > 0 && Pending.TryGetValue(go, out var p)) return p;
            if (!TryGetKey(go, out var key, out var guid)) return null;
            EnsureLoaded(guid);
            if (_indexDirty) RebuildIndex();
            return ById.TryGetValue(key, out var s) ? s : null;
        }

        /// <summary>The color a row shows: its own, or the nearest ancestor's (child colors).</summary>
        public static bool TryGetBranchColor(Transform t, out Color color, out int distance, bool includeSelf = true)
        {
            var cache = includeSelf ? BranchSelf : BranchParent;
            if (!cache.TryGetValue(t, out var b))
            {
                b = default;
                var cur = includeSelf ? t : t.parent;
                int d = includeSelf ? 0 : 1;
                // a "tinted" rule match shows its own color as a child tint
                var self = includeSelf ? null : Get(t.gameObject);
                if (self != null && self.tintOnly && self.hasColor)
                {
                    b.found = true; b.color = self.Color1; b.distance = 1;
                    cur = null;
                }
                while (cur != null)
                {
                    var s = Get(cur.gameObject);
                    if (s != null && s.hasColor && (d == 0 || s.tintChildren))
                    {
                        b.found = true; b.color = s.Color1; b.distance = s.tintOnly ? d + 1 : d;
                        break;
                    }
                    cur = cur.parent; d++;
                }
                cache[t] = b;
            }
            color = b.color; distance = b.distance;
            return b.found;
        }

        // ------------------------------------------------------------------ editing

        public static void Modify(IEnumerable<GameObject> targets, Action<ObjectStyle> edit, string undoName)
        {
            var inst = Instance;
            Undo.RecordObject(inst, undoName);
            var dirty = new HashSet<string>();

            foreach (var go in targets)
            {
                if (go == null) continue;
                if (!TryGetKey(go, out var key, out var guid))
                {
                    Pending.TryGetValue(go, out var ps);
                    if (ps == null) ps = SeedFromRule(go, new ObjectStyle());
                    edit(ps);
                    if (ps.IsEmpty) Pending.Remove(go); else Pending[go] = ps;
                    continue;
                }

                EnsureLoaded(guid);
                var scene = inst.GetScene(guid, true);
                var style = scene.objects.FirstOrDefault(o => o.id == key);
                if (style == null)
                {
                    // overriding a rule starts from the rule's look, so e.g. toggling Bold keeps its color
                    style = SeedFromRule(go, new ObjectStyle { id = key });
                    scene.objects.Add(style);
                }
                edit(style);
                if (style.IsEmpty) scene.objects.Remove(style);
                dirty.Add(guid);
            }

            _indexDirty = true;
            foreach (var g in dirty) SaveScene(g);
            EditorApplication.RepaintHierarchyWindow();
            Changed?.Invoke();
        }

        static ObjectStyle SeedFromRule(GameObject go, ObjectStyle style)
        {
            var rule = EHRules.Match(go);
            if (rule != null) style.CopyVisualsFrom(rule.style);
            return style;
        }

        /// <summary>Removes the objects' own styles (rules apply again).</summary>
        public static void Clear(IEnumerable<GameObject> targets)
        {
            Modify(targets, s =>
            {
                s.hasColor = false; s.bold = false; s.iconMode = IconMode.Default;
                s.icon = null; s.align = SeparatorAlign.Default; s.iconDarken = 0f;
            }, "Clear Hierarchy Style");
        }

        // ------------------------------------------------------------------ persistence

        SceneStyles GetScene(string guid, bool create)
        {
            var s = scenes.FirstOrDefault(x => x.sceneGuid == guid);
            if (s == null && create)
            {
                s = new SceneStyles { sceneGuid = guid };
                scenes.Add(s);
            }
            return s;
        }

        static void EnsureLoaded(string guid)
        {
            var inst = Instance;
            if (LoadedGuids.Contains(guid)) return;
            LoadedGuids.Add(guid);
            if (inst.GetScene(guid, false) != null) return;

            var scene = inst.GetScene(guid, true);
            var path = Path.Combine(Folder, guid + ".json");
            if (File.Exists(path))
            {
                try
                {
                    var file = JsonUtility.FromJson<SceneFile>(File.ReadAllText(path));
                    if (file?.objects != null) scene.objects = file.objects;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Empathy Hierarchy] Could not read {path}: {e.Message}");
                }
            }
            _indexDirty = true;
        }

        static void SaveScene(string guid)
        {
            var scene = Instance.GetScene(guid, false);
            if (scene == null) return;
            var path = Path.Combine(Folder, guid + ".json");
            try
            {
                if (scene.objects.Count == 0)
                {
                    if (File.Exists(path)) File.Delete(path);
                    return;
                }
                Directory.CreateDirectory(Folder);
                var file = new SceneFile
                {
                    objects = scene.objects.OrderBy(o => o.id, StringComparer.Ordinal).ToList()
                };
                var json = JsonUtility.ToJson(file, true);
                if (File.Exists(path))
                {
                    if ((File.GetAttributes(path) & FileAttributes.ReadOnly) != 0)
                    {
                        Debug.LogWarning($"[Empathy Hierarchy] {path} is read-only (checked out?). Style kept in memory only.");
                        return;
                    }
                    if (File.ReadAllText(path) == json) return;
                }
                File.WriteAllText(path, json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Empathy Hierarchy] Could not write {path}: {e.Message}");
            }
        }

        static void RebuildIndex()
        {
            ById.Clear();
            foreach (var s in Instance.scenes)
            foreach (var o in s.objects)
                if (!string.IsNullOrEmpty(o.id)) ById[o.id] = o;
            _indexDirty = false;
        }

        static void OnUndoRedo()
        {
            if (_instance == null) return;
            _indexDirty = true;
            foreach (var s in _instance.scenes) SaveScene(s.sceneGuid);
            EditorApplication.RepaintHierarchyWindow();
            Changed?.Invoke();
        }

        public static void NotifyReverted() => OnUndoRedo();

        static void OnSceneSaved(Scene scene)
        {
            KeyCache.Clear();
            if (Pending.Count == 0) return;

            var moved = Pending.Where(kv => kv.Key != null && kv.Key.scene == scene).ToList();
            foreach (var kv in moved)
            {
                Pending.Remove(kv.Key);
                var src = kv.Value;
                Modify(new[] { kv.Key }, s => s.CopyVisualsFrom(src), "Store Hierarchy Style");
            }
            foreach (var dead in Pending.Keys.Where(k => k == null).ToList()) Pending.Remove(dead);
        }
    }
}
