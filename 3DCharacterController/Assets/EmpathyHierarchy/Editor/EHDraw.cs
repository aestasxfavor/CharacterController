using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EmpathyHierarchy
{
    /// <summary>Drawing helpers: rounded shapes, gradients, skin colors and automatic text contrast.</summary>
    public static class EHDraw
    {
        internal static bool Pro => EditorGUIUtility.isProSkin;

        // ---- editor skin colors (Unity 6)
        public static Color WindowBg => Pro ? new Color32(56, 56, 56, 255) : new Color32(200, 200, 200, 255);
        public static Color SelectionFocused => Pro ? new Color32(44, 93, 135, 255) : new Color32(58, 114, 176, 255);
        public static Color SelectionUnfocused => Pro ? new Color32(77, 77, 77, 255) : new Color32(174, 174, 174, 255);
        public static Color Hover => Pro ? new Color32(69, 69, 69, 255) : new Color32(178, 178, 178, 255);
        public static Color DefaultText => Pro ? new Color(0.82f, 0.82f, 0.82f, 1f) : new Color(0.09f, 0.09f, 0.09f, 1f);
        public static Color PrefabText => Pro ? new Color(0.49f, 0.71f, 1f, 1f) : new Color(0.05f, 0.27f, 0.62f, 1f);
        public static Color MissingPrefabText => new Color(1f, 0.42f, 0.42f, 1f);
        public static Color StripeOverlay(float strength) =>
            Pro ? new Color(1f, 1f, 1f, strength) : new Color(0f, 0f, 0f, strength);

        // ---- shapes
        public static void Rect(Rect r, Color c)
        {
            if (c.a > 0f) EditorGUI.DrawRect(r, c);
        }

        public static void Rounded(Rect r, Color c, float radius)
        {
            if (c.a <= 0f) return;
            radius = Mathf.Min(radius, r.height * 0.5f, r.width * 0.5f);
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, 0f, radius);
        }

        public static void RoundedBorder(Rect r, Color c, float radius, float width)
        {
            if (c.a <= 0f) return;
            radius = Mathf.Min(radius, r.height * 0.5f, r.width * 0.5f);
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, width, radius);
        }

        public static void RoundedTexture(Rect r, Texture tex, float radius)
        {
            radius = Mathf.Min(radius, r.height * 0.5f, r.width * 0.5f);
            GUI.DrawTexture(r, tex, ScaleMode.StretchToFill, true, 0f, Color.white, 0f, radius);
        }

        public static void HLine(float x0, float x1, float y, Color c, float thickness = 1f)
        {
            if (x1 <= x0) return;
            Rect(new Rect(x0, y - thickness * 0.5f, x1 - x0, thickness), c);
        }

        public static void VLine(float x, float y0, float y1, Color c, float thickness = 1f)
        {
            if (y1 <= y0) return;
            Rect(new Rect(x - thickness * 0.5f, y0, thickness, y1 - y0), c);
        }

        // ---- gradients (cached 1px-high textures)
        static readonly Dictionary<long, Texture2D> GradientCache = new Dictionary<long, Texture2D>();

        public static Texture2D Gradient(Color a, Color b)
        {
            long key = ((long)ColorToInt(a) << 32) | (uint)ColorToInt(b);
            if (GradientCache.TryGetValue(key, out var tex) && tex != null) return tex;

            const int w = 64;
            tex = new Texture2D(w, 1, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            var px = new Color[w];
            for (int i = 0; i < w; i++)
            {
                float t = i / (float)(w - 1);
                t = t * t * (3f - 2f * t); // smoothstep
                px[i] = Color.Lerp(a, b, t);
            }
            tex.SetPixels(px);
            tex.Apply(false, false);
            if (GradientCache.Count > 256) GradientCache.Clear();
            GradientCache[key] = tex;
            return tex;
        }

        static int ColorToInt(Color c)
        {
            Color32 c32 = c;
            return (c32.r << 24) | (c32.g << 16) | (c32.b << 8) | c32.a;
        }

        // ---- color math
        public static Color Over(Color top, Color bottom)
        {
            float a = top.a;
            return new Color(
                top.r * a + bottom.r * (1f - a),
                top.g * a + bottom.g * (1f - a),
                top.b * a + bottom.b * (1f - a), 1f);
        }

        public static Color Opaque(Color c) { c.a = 1f; return c; }

        /// <summary>Scales a color's saturation (1 = unchanged, 0 = gray).</summary>
        public static Color Saturate(Color c, float amount)
        {
            if (amount >= 0.999f) return c;
            Color.RGBToHSV(c, out var h, out var s, out var v);
            var r = Color.HSVToRGB(h, Mathf.Clamp01(s * amount), v);
            r.a = c.a;
            return r;
        }

        public static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        public static Color Lighten(Color c, float amount) =>
            new Color(Mathf.Lerp(c.r, 1f, amount), Mathf.Lerp(c.g, 1f, amount), Mathf.Lerp(c.b, 1f, amount), c.a);

        public static Color Darken(Color c, float amount) =>
            new Color(c.r * (1f - amount), c.g * (1f - amount), c.b * (1f - amount), c.a);

        static float Channel(float c) => c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);

        /// <summary>WCAG relative luminance of a gamma-space color.</summary>
        public static float Luminance(Color c) =>
            0.2126f * Channel(c.r) + 0.7152f * Channel(c.g) + 0.0722f * Channel(c.b);

        public static float Contrast(Color a, Color b)
        {
            float la = Luminance(a), lb = Luminance(b);
            if (la < lb) { var t = la; la = lb; lb = t; }
            return (la + 0.05f) / (lb + 0.05f);
        }

        /// <summary>Black or white, whichever reads better on the background.</summary>
        public static Color ContrastText(Color background)
        {
            var white = new Color(0.97f, 0.97f, 0.97f, 1f);
            var black = new Color(0.07f, 0.07f, 0.07f, 1f);
            return Contrast(white, background) >= Contrast(black, background) ? white : black;
        }

        /// <summary>A tinted text color (brightened/darkened hue) if readable, otherwise black/white.</summary>
        public static Color TintedText(Color hue, Color background)
        {
            var tinted = Luminance(background) < 0.25f ? Lighten(hue, 0.35f) : Darken(hue, 0.45f);
            tinted.a = 1f;
            return Contrast(tinted, background) >= 3.5f ? tinted : ContrastText(background);
        }

        // ---- text styles
        static GUIStyle _label, _bold, _mini, _miniBold, _chip;

        /// <summary>Small bold centered text that clips instead of overflowing (tag/layer chips).</summary>
        public static GUIStyle Chip()
        {
            if (_chip == null)
                _chip = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    padding = new RectOffset(4, 4, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0),
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Clip,
                    fontSize = 9
                };
            return _chip;
        }

        /// <summary>Filled rect with per-corner radii (topLeft, topRight, bottomRight, bottomLeft).</summary>
        public static void RoundedCorners(Rect r, Color c, Vector4 radii)
        {
            if (c.a <= 0f) return;
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, Vector4.zero, radii);
        }
        static readonly GUIContent Temp = new GUIContent();

        public static float TextWidth(string text, GUIStyle style)
        {
            Temp.text = text;
            return style.CalcSize(Temp).x;
        }

        public static GUIStyle Label(bool bold)
        {
            if (_label == null)
            {
                _label = new GUIStyle(EditorStyles.label)
                {
                    padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0),
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Clip,
                    richText = false
                };
                _bold = new GUIStyle(_label) { font = EditorStyles.boldLabel.font, fontStyle = FontStyle.Bold };
            }
            return bold ? _bold : _label;
        }

        public static GUIStyle Mini(bool bold)
        {
            if (_mini == null)
            {
                _mini = new GUIStyle(EditorStyles.miniLabel)
                {
                    padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0),
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Overflow
                };
                _miniBold = new GUIStyle(_mini) { fontStyle = FontStyle.Bold };
            }
            return bold ? _miniBold : _mini;
        }

        public static void Text(Rect r, string text, GUIStyle style, Color color, TextAnchor anchor)
        {
            var prevColor = style.normal.textColor;
            var prevAnchor = style.alignment;
            style.normal.textColor = color;
            style.alignment = anchor;
            Temp.text = text;
            style.Draw(r, Temp, false, false, false, false);
            style.normal.textColor = prevColor;
            style.alignment = prevAnchor;
        }
    }
}
