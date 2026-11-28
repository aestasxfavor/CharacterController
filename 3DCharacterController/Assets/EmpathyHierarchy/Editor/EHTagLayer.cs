using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace EmpathyHierarchy
{
    /// <summary>
    /// Shared UI colors for the Edit Style window and popups: near-neutral charcoal layers
    /// (darkest outside, lighter inside) with Cocoa beige accents. No orange / blue / white.
    /// </summary>
    static class EHTheme
    {
        public static readonly Color Bg = Hex("#1B1A19");        // window base (darkest)
        public static readonly Color GroupBg = Hex("#252321");   // boxes (middle layer)
        public static readonly Color Card = Hex("#312E2B");      // fields / buttons (lightest layer)
        public static readonly Color CardHover = Hex("#3D3935");
        public static readonly Color Divider = new Color(0.710f, 0.561f, 0.455f, 0.18f); // #B58F74 @ 18%
        public static readonly Color Text = Hex("#E8E2DA");
        public static readonly Color Dim = Hex("#A8988A");
        public static readonly Color Accent = Hex("#D2BAAA");    // selected (bright beige)
        public static readonly Color AccentText = Hex("#3A281B");
        public static readonly Color Point = Hex("#D9B48F");
        public static readonly Color Star = Hex("#FFD21A");      // bookmark star: clear yellow, black outline     // highlight (warm caramel)

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
    }

    /// <summary>Unity-native colors for UI that lives inside the Hierarchy (navigation bar).</summary>
    static class EHEditorColors
    {
        static bool Pro => EditorGUIUtility.isProSkin;
        public static Color BarBg => Pro ? new Color(0.235f, 0.235f, 0.235f) : new Color(0.796f, 0.796f, 0.796f);
        public static Color Divider => Pro ? new Color(0.137f, 0.137f, 0.137f) : new Color(0.6f, 0.6f, 0.6f);
        public static Color Button => Pro ? new Color(0.345f, 0.345f, 0.345f) : new Color(0.894f, 0.894f, 0.894f);
        public static Color ButtonHover => Pro ? new Color(0.404f, 0.404f, 0.404f) : new Color(0.925f, 0.925f, 0.925f);
        public static Color Text => Pro ? new Color(0.824f, 0.824f, 0.824f) : new Color(0.035f, 0.035f, 0.035f);
    }

    /// <summary>Tag / layer colors, labels and editing helpers shared by both hierarchy renderers.</summary>
    [InitializeOnLoad]
    static class EHTagLayer
    {
        static readonly Dictionary<string, Color> TagColorCache = new Dictionary<string, Color>();
        static readonly Dictionary<string, Color> LayerColorCache = new Dictionary<string, Color>();
        static readonly Dictionary<(string, string), string> Tooltips = new Dictionary<(string, string), string>();

        static EHTagLayer()
        {
            // colors come from settings: rebuild when anything changes
            EHStore.Changed += () => { TagColorCache.Clear(); LayerColorCache.Clear(); };
        }

        public static string Tooltip(string tag, string layer)
        {
            var key = (tag, layer);
            if (!Tooltips.TryGetValue(key, out var t))
            {
                t = "Tag: " + tag + "\nLayer: " + layer;
                if (Tooltips.Count > 1024) Tooltips.Clear();
                Tooltips[key] = t;
            }
            return t;
        }

        static readonly Color[] Palette =
        {
            Hex("#3A8FD8"), Hex("#3DBA6B"), Hex("#E5B935"), Hex("#EB8232"), Hex("#E5484D"),
            Hex("#9B6BE8"), Hex("#2DB3A8"), Hex("#E55BA0"), Hex("#6C8CFF"), Hex("#A3C94A")
        };

        public const string DefaultTag = "Untagged";

        public static bool IsDefaultTag(string tag) => tag == DefaultTag;
        public static bool IsDefaultLayer(int layer) => layer == 0;

        public static string LayerName(int layer) => EHCache.LayerName(layer);

        public static Color TagColor(string tag)
        {
            if (!TagColorCache.TryGetValue(tag, out var c))
            {
                c = ColorFor(tag, EHSettings.instance.tagColors);
                TagColorCache[tag] = c;
            }
            return c;
        }

        public static Color LayerColor(int layer)
        {
            var name = LayerName(layer);
            if (!LayerColorCache.TryGetValue(name, out var c))
            {
                c = ColorFor(name, EHSettings.instance.layerColors);
                LayerColorCache[name] = c;
            }
            return c;
        }

        static Color ColorFor(string name, List<NamedColor> overrides)
        {
            if (overrides != null)
                foreach (var o in overrides)
                    if (o != null && o.name == name) return EHDraw.Opaque(o.color);
            return Palette[(int)(Fnv(name) % (uint)Palette.Length)];
        }

        // stable across sessions (string.GetHashCode is not guaranteed to be)
        static uint Fnv(string s)
        {
            uint h = 2166136261;
            foreach (char c in s ?? "") { h ^= c; h *= 16777619; }
            return h;
        }

        /// <summary>Chip colors: real color for non-default values, a quiet neutral chip for defaults.</summary>
        public static void ChipColors(bool isDefault, Color color, Color under, out Color bg, out Color text)
        {
            if (isDefault)
            {
                bg = EHDraw.Luminance(under) > 0.35f ? new Color(0f, 0f, 0f, 0.10f) : new Color(1f, 1f, 1f, 0.08f);
                text = EHDraw.WithAlpha(EHDraw.ContrastText(EHDraw.Over(bg, under)), 0.55f);
            }
            else
            {
                bg = color;
                text = EHDraw.ContrastText(EHDraw.Over(bg, under));
            }
        }

        public static bool ShouldShow(GameObject go, EHSettings s)
        {
            if (s.tagLayerStyle == TagLayerStyle.Off) return false;
            if (s.showDefaultTagLayer) return true;
            return !IsDefaultTag(EHCache.Tag(go)) || !IsDefaultLayer(go.layer);
        }

        public static GameObject[] Targets(GameObject go) =>
            Selection.Contains(go) ? Selection.gameObjects : new[] { go };

        public static void SetTag(GameObject[] targets, string tag)
        {
            Undo.RecordObjects(targets, "Set Tag");
            foreach (var g in targets) if (g != null) g.tag = tag;
            Changed();
        }

        public static void SetLayer(GameObject[] targets, int layer)
        {
            Undo.RecordObjects(targets, "Set Layer");
            foreach (var g in targets) if (g != null) g.layer = layer;
            Changed();
        }

        static void Changed()
        {
            EditorApplication.RepaintHierarchyWindow();
            EHStore.NotifyChanged();
        }

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.gray;
    }

    /// <summary>
    /// Themed dropdown for picking a tag and/or a layer. Picking keeps the popup open
    /// (tag and layer can both be changed in one go); click outside or press Esc to close.
    /// </summary>
    public class EHTagLayerPopup : EditorWindow
    {
        public enum Mode { Both, Tag, Layer }

        const float W = 230f, RowH = 22f, Pad = 8f, HeaderH = 22f, FooterH = 34f;

        GameObject[] _targets;
        Mode _mode;
        Vector2 _scroll;
        List<int> _layers;
        static readonly GUIContent Temp = new GUIContent();
        GUIStyle _row, _section, _link;

        public static void Open(Rect screenRect, GameObject[] targets, Mode mode)
        {
            if (targets == null || targets.Length == 0) return;
            var w = CreateInstance<EHTagLayerPopup>();
            w._targets = targets;
            w._mode = mode;
            w._layers = Layers();
            w.wantsMouseMove = true;
            w.ShowAsDropDown(screenRect, new Vector2(W, w.CalcHeight()));
        }

        static List<int> Layers()
        {
            var list = new List<int>();
            for (int i = 0; i < 32; i++)
                if (!string.IsNullOrEmpty(LayerMask.LayerToName(i))) list.Add(i);
            return list;
        }

        float ContentHeight()
        {
            float h = Pad;
            if (_mode != Mode.Layer) h += HeaderH + InternalEditorUtility.tags.Length * RowH;
            if (_mode == Mode.Both) h += 8f;
            if (_mode != Mode.Tag) h += HeaderH + _layers.Count * RowH;
            return h + Pad;
        }

        float CalcHeight() => Mathf.Min(ContentHeight() + FooterH, 460f);

        void Styles()
        {
            if (_row != null) return;
            _row = new GUIStyle(EditorStyles.label) { fontSize = 12, alignment = TextAnchor.MiddleLeft, normal = { textColor = EHTheme.Text } };
            _section = new GUIStyle(EditorStyles.miniBoldLabel) { fontSize = 10, normal = { textColor = EHTheme.Dim } };
            _link = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = EHTheme.Dim } };
        }

        void OnGUI()
        {
            if (_targets == null) { Close(); return; }
            Styles();
            var e = Event.current;
            if (e.type == EventType.MouseMove) Repaint();
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { Close(); e.Use(); return; }

            var full = new Rect(0, 0, position.width, position.height);
            EditorGUI.DrawRect(full, EHTheme.Bg);
            EHDraw.RoundedBorder(new Rect(0.5f, 0.5f, full.width - 1f, full.height - 1f), EHTheme.Divider, 6f, 1f);

            var listRect = new Rect(0, 0, full.width, full.height - FooterH);
            var content = new Rect(0, 0, full.width - 14f, ContentHeight());
            _scroll = GUI.BeginScrollView(listRect, _scroll, content);
            float y = Pad;
            float w = content.width;

            if (_mode != Mode.Layer)
            {
                GUI.Label(new Rect(Pad + 4f, y + 4f, w, 16f), "TAG", _section);
                y += HeaderH;
                foreach (var tag in InternalEditorUtility.tags)
                {
                    int count = 0;
                    foreach (var g in _targets) if (g != null && g.CompareTag(tag)) count++;
                    if (Row(new Rect(Pad, y, w - Pad, RowH - 2f), tag, EHTagLayer.TagColor(tag),
                            EHTagLayer.IsDefaultTag(tag), count, _targets.Length))
                        EHTagLayer.SetTag(_targets, tag);
                    y += RowH;
                }
            }

            if (_mode == Mode.Both)
            {
                EHDraw.Rect(new Rect(Pad, y + 3f, w - Pad, 1f), EHTheme.Divider);
                y += 8f;
            }

            if (_mode != Mode.Tag)
            {
                GUI.Label(new Rect(Pad + 4f, y + 4f, w, 16f), "LAYER", _section);
                y += HeaderH;
                foreach (var layer in _layers)
                {
                    int count = 0;
                    foreach (var g in _targets) if (g != null && g.layer == layer) count++;
                    if (Row(new Rect(Pad, y, w - Pad, RowH - 2f), EHTagLayer.LayerName(layer), EHTagLayer.LayerColor(layer),
                            EHTagLayer.IsDefaultLayer(layer), count, _targets.Length))
                        EHTagLayer.SetLayer(_targets, layer);
                    y += RowH;
                }
            }
            GUI.EndScrollView();

            // footer
            var footer = new Rect(Pad, full.height - FooterH + 5f, full.width - Pad * 2f, FooterH - 10f);
            EHDraw.Rect(new Rect(Pad, full.height - FooterH, full.width - Pad * 2f, 1f), EHTheme.Divider);
            bool hover = footer.Contains(e.mousePosition);
            if (e.type == EventType.Repaint) EHDraw.Rounded(footer, hover ? EHTheme.CardHover : EHTheme.Card, 6f);
            GUI.Label(footer, "Edit Tags & Layers…", _link);
            if (GUI.Button(footer, GUIContent.none, GUIStyle.none))
            {
                SettingsService.OpenProjectSettings("Project/Tags and Layers");
                Close();
                GUIUtility.ExitGUI();
            }
        }

        bool Row(Rect r, string label, Color color, bool isDefault, int count, int total)
        {
            var e = Event.current;
            bool hover = r.Contains(e.mousePosition);
            bool all = count == total && total > 0;
            bool some = count > 0 && !all;

            if (e.type == EventType.Repaint)
            {
                if (all) EHDraw.Rounded(r, EHDraw.WithAlpha(EHTheme.Accent, 0.14f), 6f);
                else if (hover) EHDraw.Rounded(r, EHTheme.CardHover, 6f);

                var dot = new Rect(r.x + 8f, r.center.y - 5f, 10f, 10f);
                if (isDefault) EHDraw.RoundedBorder(dot, EHTheme.Dim, 5f, 1.5f);
                else EHDraw.Rounded(dot, color, 5f);

                var prev = _row.normal.textColor;
                _row.normal.textColor = isDefault ? EHTheme.Dim : EHTheme.Text;
                _row.fontStyle = all ? FontStyle.Bold : FontStyle.Normal;
                Temp.text = label;
                _row.Draw(new Rect(r.x + 26f, r.y, r.width - 50f, r.height), Temp, false, false, false, false);
                _row.normal.textColor = prev;
                _row.fontStyle = FontStyle.Normal;

                if (all || some)
                    EHDraw.Text(new Rect(r.xMax - 24f, r.y, 18f, r.height), all ? "✓" : "–",
                        EHDraw.Label(true), EHTheme.Accent, TextAnchor.MiddleCenter);
            }
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }
    }
}
