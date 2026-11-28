using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EmpathyHierarchy
{
    /// <summary>
    /// Caches values that Unity allocates a new string/object for on every call
    /// (GameObject.name, GameObject.tag, LayerToName, number formatting, default icons),
    /// so drawing a Hierarchy row doesn't allocate. Cleared whenever the hierarchy or a style changes.
    /// </summary>
    [InitializeOnLoad]
    static class EHCache
    {
        static readonly Dictionary<GameObject, string> Names = new Dictionary<GameObject, string>();
        static readonly Dictionary<GameObject, string> Tags = new Dictionary<GameObject, string>();
        static readonly Dictionary<GameObject, string> SepLabels = new Dictionary<GameObject, string>();
        static readonly char[] SeparatorTrim = " -=/_*#~.|".ToCharArray();
        static readonly Dictionary<GameObject, Texture> DefaultIcons = new Dictionary<GameObject, Texture>();
        static readonly Dictionary<long, string> Pairs = new Dictionary<long, string>();
        static readonly Dictionary<long, string> CountTips = new Dictionary<long, string>();
        static readonly string[] LayerNames = new string[32];
        static readonly string[] Numbers = new string[1000];
        static bool _layersValid;

        static EHCache()
        {
            EditorApplication.hierarchyChanged += Clear;
            EHStore.Changed += Clear;
            EditorApplication.projectChanged += () => _layersValid = false;
        }

        static void Clear()
        {
            Names.Clear();
            Tags.Clear();
            SepLabels.Clear();
            DefaultIcons.Clear();
            _layersValid = false;
        }

        public static string Name(GameObject go)
        {
            if (!Names.TryGetValue(go, out var n)) { n = go.name; Names[go] = n; }
            return n;
        }

        /// <summary>Separator name without its decoration characters ("--- UI ---" → "UI").</summary>
        public static string SeparatorLabel(GameObject go)
        {
            if (!SepLabels.TryGetValue(go, out var l)) { l = Name(go).Trim(SeparatorTrim); SepLabels[go] = l; }
            return l;
        }

        public static string Tag(GameObject go)
        {
            if (!Tags.TryGetValue(go, out var t)) { t = go.tag; Tags[go] = t; }
            return t;
        }

        public static string LayerName(int layer)
        {
            if (!_layersValid)
            {
                for (int i = 0; i < 32; i++)
                {
                    var n = LayerMask.LayerToName(i);
                    LayerNames[i] = string.IsNullOrEmpty(n) ? "Layer " + i : n;
                }
                _layersValid = true;
            }
            return LayerNames[layer & 31];
        }

        public static Texture DefaultIcon(GameObject go)
        {
            if (!DefaultIcons.TryGetValue(go, out var tex))
            {
                tex = EditorGUIUtility.ObjectContent(go, typeof(GameObject)).image;
                DefaultIcons[go] = tex;
            }
            return tex;
        }

        public static string Number(int n)
        {
            if (n < 0 || n >= Numbers.Length) return n.ToString();
            return Numbers[n] ?? (Numbers[n] = n.ToString());
        }

        /// <summary>"direct · total" without allocating after the first time.</summary>
        public static string Pair(int a, int b)
        {
            long key = ((long)a << 32) | (uint)b;
            if (!Pairs.TryGetValue(key, out var s))
            {
                s = a + " · " + b;
                if (Pairs.Count > 4096) Pairs.Clear();
                Pairs[key] = s;
            }
            return s;
        }

        public static string CountTooltip(int direct, int total)
        {
            long key = ((long)direct << 32) | (uint)total;
            if (!CountTips.TryGetValue(key, out var s))
            {
                s = "Children: " + direct + "\nAll descendants: " + total;
                if (CountTips.Count > 4096) CountTips.Clear();
                CountTips[key] = s;
            }
            return s;
        }
    }
}
