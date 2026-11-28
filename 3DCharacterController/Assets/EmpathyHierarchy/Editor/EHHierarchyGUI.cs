using UnityEditor;
using UnityEngine;

namespace EmpathyHierarchy
{
    /// <summary>
    /// Draws every Hierarchy row: striped rows, tree lines, colored pills (solid / soft / outline,
    /// gradients), child colors, bold, custom icons, separators, root outlines,
    /// child-count badges and the active checkbox.
    /// Uses the EntityId callback on 6.4+ and the InstanceID one on 6.3, so no obsolete APIs are touched.
    /// </summary>
    [InitializeOnLoad]
    static class EHHierarchyGUI
    {
        const float Indent = 14f;
        const float GutterLeft = 32f; // visibility / pickability column


        static readonly System.Collections.Generic.Dictionary<Transform, int> TotalCache =
            new System.Collections.Generic.Dictionary<Transform, int>();

        static EHHierarchyGUI()
        {
            EditorApplication.hierarchyChanged += TotalCache.Clear;
#if UNITY_6000_4_OR_NEWER
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI -= OnItem;
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI += OnItem;
#else
            // 6.3 only has the int InstanceID callback (the EntityId one arrives in 6.4)
            EditorApplication.hierarchyWindowItemOnGUI -= OnItemInstanceId;
            EditorApplication.hierarchyWindowItemOnGUI += OnItemInstanceId;
#endif
        }

#if !UNITY_6000_4_OR_NEWER
        static void OnItemInstanceId(int instanceId, Rect rect) => OnItem(instanceId, rect);
#endif

        internal static int CallCount;

        static GUIContent _sceneButton;

        static readonly GUIContent SceneNameContent = new GUIContent();

        /// <summary>The scene this header row belongs to (matched by handle), or null.</summary>
        static string SceneTitle(EntityId id)
        {
            string key = id.ToString();
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var sc = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (sc.handle.ToString() == key)
                    return (string.IsNullOrEmpty(sc.name) ? "Untitled" : sc.name) + (sc.isDirty ? "*" : "");
            }
            if (UnityEngine.SceneManagement.SceneManager.sceneCount == 1)
            {
                var sc = UnityEngine.SceneManagement.SceneManager.GetSceneAt(0);
                return (string.IsNullOrEmpty(sc.name) ? "Untitled" : sc.name) + (sc.isDirty ? "*" : "");
            }
            return null;
        }

        enum RowButton { Rules, Memo, Collapse }
        static readonly GUIContent[] RowButtonTips =
        {
            new GUIContent(string.Empty, "Rules  (automatic looks by name or component)"),
            new GUIContent(string.Empty, "Memo Desk  (drag objects from the Hierarchy into it)"),
            new GUIContent(string.Empty, "Collapse all  (Alt+Shift+C)")
        };

        /// <summary>Beige Rules / Memo / Collapse buttons on a scene row (classic Hierarchy). Returns their left edge.</summary>
        static float SceneRowButtons(Rect rect)
        {
            const float bw = 17f, bh = 14f, gap = 2f, menuW = 17f; // menuW: room for Unity's ⋮
            float x = rect.xMax - menuW - 3 * bw - 2 * gap;
            float y = rect.y + (rect.height - bh) * 0.5f;
            var e = Event.current;
            for (int i = 0; i < 3; i++)
            {
                var r = new Rect(x + i * (bw + gap), y, bw, bh);
                bool hover = r.Contains(e.mousePosition);
                if (e.type == EventType.Repaint)
                {
                    EHDraw.Rounded(r, hover ? EHDraw.Lighten(EHTheme.Accent, 0.12f) : EHTheme.Accent, 3.5f);
                    DrawRowButtonIcon((RowButton)i, r.center, EHTheme.AccentText);
                }
                GUI.Label(r, RowButtonTips[i], GUIStyle.none); // tooltip
                if (e.type == EventType.MouseDown && e.button == 0 && hover)
                {
                    e.Use();
                    var which = (RowButton)i;
                    EditorApplication.delayCall += () =>
                    {
                        if (which == RowButton.Rules) EHRulesWindow.Open(null);
                        else if (which == RowButton.Memo) EHMemoDesk.Open();
                        else EHMenus.CollapseAll();
                    };
                }
            }
            return x;
        }

        /// <summary>Same glyphs as the new Hierarchy's header buttons, drawn with plain rects.</summary>
        static void DrawRowButtonIcon(RowButton which, Vector2 c, Color col)
        {
            switch (which)
            {
                case RowButton.Rules:
                    for (int i = 0; i < 3; i++)
                    {
                        float y = Mathf.Round(c.y - 3.5f + i * 3.5f);
                        EHDraw.Rect(new Rect(c.x - 4.5f, y - 1f, 2f, 2f), col);
                        EHDraw.Rect(new Rect(c.x - 1.5f, y - 0.75f, 6f, 1.5f), col);
                    }
                    break;
                case RowButton.Memo:
                {
                    var page = new Rect(Mathf.Round(c.x - 4f), Mathf.Round(c.y - 5f), 8f, 10f);
                    EHDraw.Rect(new Rect(page.x, page.y, page.width, 1.3f), col);
                    EHDraw.Rect(new Rect(page.x, page.yMax - 1.3f, page.width, 1.3f), col);
                    EHDraw.Rect(new Rect(page.x, page.y, 1.3f, page.height), col);
                    EHDraw.Rect(new Rect(page.xMax - 1.3f, page.y, 1.3f, page.height), col);
                    for (int i = 0; i < 3; i++)
                        EHDraw.Rect(new Rect(page.x + 2.2f, page.y + 2.5f + i * 2.2f, i == 2 ? 2.5f : 3.5f, 1.1f), col);
                    break;
                }
                default:
                {
                    EHDraw.Rect(new Rect(c.x - 4f, c.y + 3.5f, 8f, 1.5f), col);          // baseline
                    EHDraw.Rect(new Rect(c.x - 0.75f, c.y - 1.5f, 1.5f, 4f), col);       // shaft
                    for (int i = 0; i < 4; i++)                                          // arrow head
                    {
                        float w = 1.5f + i * 1.6f;
                        EHDraw.Rect(new Rect(c.x - w * 0.5f, c.y - 4.5f + i * 1.05f, w, 1.2f), col);
                    }
                    break;
                }
            }
        }

        static void SceneButton(EntityId id, Rect rect, float rightLimit)
        {
            if (_sceneButton == null) _sceneButton = new GUIContent("▾", "Scenes  (Alt + Shift + S)");
            var e = Event.current;
            // right after the scene's name (like the new Hierarchy); at the right end if the scene can't be told
            float x = rightLimit - 22f;
            string title = SceneTitle(id);
            if (title != null)
            {
                SceneNameContent.text = title;
                float w = EditorStyles.boldLabel.CalcSize(SceneNameContent).x;
                x = Mathf.Min(rect.x + 18f + w + 2f, rightLimit - 22f);
            }
            var r = new Rect(x, rect.y + 1f, 18f, rect.height - 2f);
            bool hover = r.Contains(e.mousePosition);
            if (e.type == EventType.Repaint)
            {
                if (hover) EHDraw.Rounded(r, EHDraw.WithAlpha(EHDraw.Pro ? Color.white : Color.black, 0.12f), 4f);
                EHDraw.Text(r, "▾", EHDraw.Label(false), EHDraw.Pro ? new Color(0.82f, 0.82f, 0.82f) : new Color(0.1f, 0.1f, 0.1f),
                    TextAnchor.MiddleCenter);
            }
            if (e.type == EventType.MouseDown && e.button == 0 && hover)
            {
                EHScenePopup.Open(GUIUtility.GUIToScreenRect(r));
                e.Use();
            }
        }

        static void OnItem(EntityId entityId, Rect rect)
        {
            CallCount++;
            EHSettings.NoteHierarchy(false);
            var s = EHSettings.instance;
            if (!s.enabled) return;

            var go = EditorUtility.EntityIdToObject(entityId) as GameObject;
            if (go == null)
            {
                // scene header: Rules / Memo / Collapse at the right (left of Unity's ⋮ menu), ▾ after the name
                float left = SceneRowButtons(rect);
                if (s.sceneSelector) SceneButton(entityId, rect, left);
                return;
            }

            var e = Event.current;
            if (e.alt && (e.type == EventType.ContextClick || e.type == EventType.MouseDown && e.button == 1) &&
                e.mousePosition.y >= rect.yMin && e.mousePosition.y < rect.yMax)
            {
                // Alt + right-click: Edit Style instead of Unity's context menu (works where Alt+E is taken by the menu bar)
                bool down = e.type == EventType.MouseDown; // read before Use(), which turns the type into Used
                e.Use();
                if (down)
                {
                    var targets = Selection.Contains(go) ? Selection.gameObjects : new[] { go };
                    if (!Selection.Contains(go)) Selection.activeGameObject = go;
                    EditorApplication.delayCall += () => EHStyleWindow.Open(targets);
                }
                return;
            }
            // Alt + Click on the row (not the arrow, which keeps Unity's "unfold everything"): add / remove bookmark
            if (e.alt && e.type == EventType.MouseDown && e.button == 0 && s.navBar &&
                e.mousePosition.y >= rect.yMin && e.mousePosition.y < rect.yMax && e.mousePosition.x >= rect.x)
            {
                e.Use();
                EHNav.ToggleFromHierarchy(go, EHLegacyHierarchy.Notify);
                return;
            }
            bool repaint = e.type == EventType.Repaint;
            var t = go.transform;
            int depth = Depth(t);
            float baseX = rect.x - depth * Indent;
            float h = rect.height;
            float right = rect.xMax - s.rightPadding;
            var full = new Rect(GutterLeft, rect.y, Mathf.Max(0f, rect.xMax - GutterLeft), h);

            bool selected = Selection.Contains(go);
            bool hover = full.Contains(e.mousePosition);
            Color rowBg = selected
                ? (HierarchyFocused() ? EHDraw.SelectionFocused : EHDraw.SelectionUnfocused)
                : hover ? EHDraw.Hover : EHDraw.WindowBg;

            bool odd = (Mathf.RoundToInt(rect.y / Mathf.Max(1f, h)) & 1) == 1;
            Color stripe = EHDraw.StripeOverlay(s.stripeStrength);
            bool striped = s.stripedRows && odd && !selected;
            Color baseBg = striped ? EHDraw.Over(stripe, rowBg) : rowBg;

            var style = EHStore.Get(go);

            // ---- right side layout (right to left)
            float contentRight = right;
            Rect toggleRect = default, badgeRect = default;
            string countText = null;
            bool isSep = IsSeparator(EHCache.Name(go), s);
            bool showToggle = s.activeToggle && !isSep; // separators are labels, not objects to switch
            if (showToggle)
            {
                float tw = s.activeToggleStyle == ActiveToggleStyle.Switch ? 26f : 16f;
                toggleRect = new Rect(right - tw, rect.y, tw, h);
                contentRight = toggleRect.x - 2f;
            }

            // ---- tag / layer chip
            bool showTL = !isSep && EHTagLayer.ShouldShow(go, s);
            Rect tlRect = default, tlTagRect = default, tlLayRect = default;
            string tagText = null, layText = null;
            if (showTL)
            {
                if (s.tagLayerStyle == TagLayerStyle.Combined)
                {
                    string tag = EHCache.Tag(go);
                    tagText = EHTagLayer.IsDefaultTag(tag) ? "TAG" : tag;
                    layText = EHTagLayer.IsDefaultLayer(go.layer) ? "LAY" : EHCache.LayerName(go.layer);
                    float halfW = s.tagLayerMaxWidth * 0.5f;
                    float tw = Mathf.Min(EHDraw.TextWidth(tagText, EHDraw.Chip()) + 2f, halfW);
                    float lw = Mathf.Min(EHDraw.TextWidth(layText, EHDraw.Chip()) + 2f, halfW);
                    tlRect = new Rect(contentRight - tw - lw, rect.y + 2f, tw + lw, h - 4f);
                    tlTagRect = new Rect(tlRect.x, tlRect.y, tw, tlRect.height);
                    tlLayRect = new Rect(tlRect.x + tw, tlRect.y, lw, tlRect.height);
                }
                else
                {
                    tlLayRect = new Rect(contentRight - 14f, rect.y + (h - 14f) * 0.5f, 14f, 14f);
                    tlTagRect = new Rect(tlLayRect.x - 16f, tlLayRect.y, 14f, 14f);
                    tlRect = new Rect(tlTagRect.x, tlTagRect.y, 30f, 14f);
                }
                contentRight = tlRect.x - 4f;
            }

            // child count sits left of the tag/layer chip so the chips line up in one column
            if (s.childCountBadge && t.childCount > 0)
            {
                countText = CountText(t, null, s, out _);
                float w = Mathf.Max(18f, EHDraw.TextWidth(countText, EHDraw.Mini(true)) + 10f);
                badgeRect = new Rect(contentRight - w, rect.y + 2f, w, h - 4f);
                contentRight = badgeRect.x - 4f;
            }

            Color underBadge = baseBg;
            if (repaint)
            {
                TrackExpanded(go, rect);
                if (striped) EHDraw.Rect(full, stripe);
                if (s.treeLines && depth > 0) DrawTreeLines(t, depth, baseX, rect, s);

                if (isSep)
                    underBadge = DrawSeparator(go, style, rect, full, baseBg, selected, s, contentRight);
                else
                    underBadge = DrawRow(go, t, style, rect, full, baseBg, selected, hover, depth, s, contentRight);

                if (t.childCount > 0 && (s.brightArrows || s.arrowBadges))
                    DrawFoldoutArrow(go, rect, baseBg, s);
                if (s.navBar && s.navStarOnRows && EHBookmarks.Contains(go) && !isSep)
                {
                    bool bold = (style != null && style.bold) || (s.boldRootObjects && depth == 0);
                    float x = rect.x + 18f + EHDraw.TextWidth(EHCache.Name(go), EHDraw.Label(bold)) + 2f;
                    DrawStar(new Rect(Mathf.Min(x, contentRight - 17f), rect.y, 17f, h));
                }
                if (countText != null) DrawBadge(badgeRect, countText, underBadge, RowColor(t), s);
                if (showTL) DrawTagLayer(go, s, tlTagRect, tlLayRect, tagText, layText, underBadge);
            }

            if (showTL && e.type == EventType.MouseDown && e.button == 0 && tlRect.Contains(e.mousePosition))
            {
                var mode = s.tagLayerStyle == TagLayerStyle.Combined ? EHTagLayerPopup.Mode.Both
                    : tlTagRect.Contains(e.mousePosition) ? EHTagLayerPopup.Mode.Tag : EHTagLayerPopup.Mode.Layer;
                EHTagLayerPopup.Open(GUIUtility.GUIToScreenRect(tlRect), EHTagLayer.Targets(go), mode);
                e.Use();
            }

            if (showToggle) DrawActiveToggle(go, toggleRect, s, underBadge, RowColor(t));
        }

        static GUIStyle _starStyle;
        static readonly GUIContent StarContent = new GUIContent("★");

        /// <summary>Bookmark ★ right after the name: clear yellow, black outline (drawn as 8 offset copies).</summary>
        static void DrawStar(Rect r)
        {
            if (_starStyle == null)
                _starStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 15, alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0)
                };
            var n = _starStyle.normal;
            n.textColor = Color.black;
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                if (dx != 0 || dy != 0) _starStyle.Draw(new Rect(r.x + dx, r.y + dy, r.width, r.height), StarContent, false, false, false, false);
            n.textColor = EHTheme.Star;
            _starStyle.Draw(r, StarContent, false, false, false, false);
        }

        // ------------------------------------------------------------------ rows

        static Color DrawRow(GameObject go, Transform t, ObjectStyle style, Rect rect, Rect full, Color baseBg,
            bool selected, bool hover, int depth, EHSettings s, float contentRight)
        {
            bool own = style != null && style.hasColor && !style.tintOnly;
            Color branch = default;
            int dist = 0;
            bool inherited = !own && s.childColors && EHStore.TryGetBranchColor(t, out branch, out dist, false);
            bool bold = (style != null && style.bold) || (s.boldRootObjects && depth == 0);
            bool customIcon = EHIcons.HasCustomIcon(style, s.componentAwareIcons);
            bool outlineRoot = depth == 0 &&
                               (s.rootOutline == RootOutlineMode.AllRoots ||
                                (s.rootOutline == RootOutlineMode.ColoredRoots && own));

            Color under = baseBg;

            if (own || inherited || bold || customIcon)
            {
                // cover Unity's default label, then redraw our own
                EHDraw.Rect(new Rect(rect.x - 1f, rect.y, rect.xMax - rect.x + 1f, rect.height), baseBg);

                var pill = new Rect(rect.x - 2f, rect.y + 1f, rect.xMax - rect.x + 2f, rect.height - 2f);
                float r = s.rowRadius;
                Color textColor;

                if (own)
                {
                    Color c = EHDraw.Opaque(style.Color1);
                    Color fa = FillColor(c, style.fill, baseBg);
                    Color fb = style.gradient == GradientMode.TwoColor
                        ? FillColor(EHDraw.Opaque(style.Color2), style.fill, baseBg)
                        : baseBg;
                    if (selected) { fa = Color.Lerp(fa, baseBg, 0.35f); fb = Color.Lerp(fb, baseBg, 0.35f); }
                    else if (hover) { fa = EHDraw.Lighten(fa, 0.06f); fb = EHDraw.Lighten(fb, 0.06f); }

                    if (style.gradient == GradientMode.None) EHDraw.Rounded(pill, fa, r);
                    else EHDraw.RoundedTexture(pill, EHDraw.Gradient(fa, fb), r);

                    if (style.fill != FillMode.Solid)
                        EHDraw.RoundedBorder(pill, EHDraw.WithAlpha(c, style.fill == FillMode.Outline ? 0.9f : 0.45f), r, 1f);
                    if (selected)
                        EHDraw.RoundedBorder(pill, EHDraw.Lighten(EHDraw.SelectionFocused, 0.35f), r, 1f);
                    if (s.accentBar)
                    {
                        Color bar = style.fill == FillMode.Solid ? EHDraw.Lighten(c, 0.45f) : c;
                        EHDraw.Rounded(new Rect(pill.x, pill.y, 3f, pill.height), bar, 1.5f);
                    }

                    textColor = style.fill == FillMode.Solid ? EHDraw.ContrastText(fa) : EHDraw.TintedText(c, fa);
                    under = style.gradient == GradientMode.None ? fa : fb;
                }
                else if (inherited)
                {
                    float a = s.childTintAlpha * Mathf.Pow(s.childTintFade, Mathf.Max(0, dist - 1));
                    Color tint = EHDraw.Over(EHDraw.WithAlpha(branch, a), baseBg);
                    EHDraw.Rounded(pill, tint, r);
                    if (s.accentBar)
                        EHDraw.Rounded(new Rect(pill.x, pill.y, 2f, pill.height), EHDraw.WithAlpha(branch, 0.7f), 1f);
                    textColor = DefaultTextFor(go, tint);
                    under = tint;
                }
                else
                {
                    textColor = DefaultTextFor(go, baseBg);
                }

                bool active = go.activeInHierarchy;
                if (!active) textColor.a *= 0.5f;

                var tex = EHIcons.Resolve(go, style, s.componentAwareIcons);
                if (tex != null)
                {
                    var iconRect = new Rect(rect.x, rect.y + (rect.height - 16f) * 0.5f, 16f, 16f);
                    Color iconTint = style != null ? style.IconTint : Color.white;
                    iconTint.a = active ? 1f : 0.5f;
                    GUI.DrawTexture(iconRect, tex, ScaleMode.ScaleToFit, true, 0f, iconTint, 0f, 0f);
                }

                var textRect = new Rect(rect.x + 18f, rect.y, Mathf.Max(0f, contentRight - rect.x - 18f), rect.height);
                EHDraw.Text(textRect, EHCache.Name(go), EHDraw.Label(bold), textColor, TextAnchor.MiddleLeft);
            }

            if (outlineRoot)
            {
                Color oc = own ? EHDraw.WithAlpha(EHDraw.Opaque(style.Color1), 0.9f) : s.outlineColor;
                float x0 = s.outlineFullWidth ? full.x : rect.x - 2f + s.rowRadius;
                float x1 = s.outlineFullWidth ? full.xMax : rect.xMax - s.rowRadius;
                EHDraw.HLine(x0, x1, rect.y + 0.5f, oc);
                EHDraw.HLine(x0, x1, rect.yMax - 0.5f, oc);
            }

            return under;
        }

        internal static Color FillColor(Color c, FillMode mode, Color bg)
        {
            switch (mode)
            {
                case FillMode.Solid: return c;
                case FillMode.Soft: return Color.Lerp(bg, c, EditorGUIUtility.isProSkin ? 0.30f : 0.38f);
                default: return bg; // Outline
            }
        }

        static Color DefaultTextFor(GameObject go, Color bg)
        {
            Color c = EHDraw.DefaultText;
            var status = PrefabUtility.GetPrefabInstanceStatus(go);
            if (status == PrefabInstanceStatus.Connected) c = EHDraw.PrefabText;
            else if (status == PrefabInstanceStatus.MissingAsset) c = EHDraw.MissingPrefabText;
            return EHDraw.Contrast(c, bg) >= 3f ? c : EHDraw.ContrastText(bg);
        }

        // ------------------------------------------------------------------ separators

        public static bool IsSeparator(string name, EHSettings s)
        {
            foreach (var p in s.SeparatorPrefixList)
                if (name.StartsWith(p, System.StringComparison.Ordinal)) return true;
            return false;
        }

        static Color DrawSeparator(GameObject go, ObjectStyle style, Rect rect, Rect full, Color baseBg,
            bool selected, EHSettings s, float contentRight)
        {
            EHDraw.Rect(new Rect(rect.x - 1f, rect.y, rect.xMax - rect.x + 1f, rect.height), baseBg);

            string label = EHCache.SeparatorLabel(go);
            var align = style != null && style.align != SeparatorAlign.Default ? style.align : s.separatorAlign;
            if (align == SeparatorAlign.Default) align = SeparatorAlign.Center;

            bool own = style != null && style.hasColor && !style.tintOnly;
            Color c = own ? EHDraw.Opaque(style.Color1) : EHDraw.Opaque(s.outlineColor);
            Color line = own ? EHDraw.WithAlpha(c, 0.9f) : s.outlineColor;

            var inner = new Rect(rect.x + 1f, rect.y, Mathf.Max(0f, contentRight - rect.x - 1f), rect.height);
            var st = EHDraw.Label(true);
            float tw = label.Length > 0 ? EHDraw.TextWidth(label, st) : 0f;
            float tx = align == SeparatorAlign.Left ? inner.x + 8f
                : align == SeparatorAlign.Right ? inner.xMax - 8f - tw
                : inner.center.x - tw * 0.5f;
            tx = Mathf.Max(inner.x + 2f, tx);

            Color under = baseBg;
            Color textColor;
            bool solid = own && style.fill == FillMode.Solid;

            if (solid)
            {
                var pill = new Rect(inner.x - 3f, rect.y + 1f, inner.width + 3f, rect.height - 2f);
                Color fa = selected ? Color.Lerp(c, baseBg, 0.35f) : c;
                Color fb = style.gradient == GradientMode.TwoColor ? EHDraw.Opaque(style.Color2)
                    : style.gradient == GradientMode.Fade ? baseBg : fa;
                if (style.gradient == GradientMode.None) EHDraw.Rounded(pill, fa, s.rowRadius);
                else EHDraw.RoundedTexture(pill, EHDraw.Gradient(fa, fb), s.rowRadius);
                textColor = EHDraw.ContrastText(fa);
                under = fb;
            }
            else
            {
                var box = new Rect(inner.x - 3f, rect.y + 1f, inner.width + 3f, rect.height - 2f);
                if (own && style.fill == FillMode.Soft)
                {
                    under = Color.Lerp(baseBg, c, EditorGUIUtility.isProSkin ? 0.30f : 0.38f);
                    EHDraw.Rounded(box, under, s.rowRadius);
                }
                else if (own && style.fill == FillMode.Outline)
                {
                    EHDraw.RoundedBorder(box, EHDraw.WithAlpha(c, 0.9f), s.rowRadius, 1f);
                }
                float y = rect.center.y;
                const float pad = 6f;
                if (tw > 0f)
                {
                    EHDraw.HLine(inner.x, tx - pad, y, line);
                    EHDraw.HLine(tx + tw + pad, inner.xMax, y, line);
                }
                else EHDraw.HLine(inner.x, inner.xMax, y, line);
                textColor = own ? EHDraw.TintedText(c, under) : EHDraw.ContrastText(under);
            }

            if (tw > 0f)
            {
                EHDraw.Text(new Rect(tx, rect.y, tw + 2f, rect.height), label, st, textColor, TextAnchor.MiddleLeft);
                if (s.separatorOutline)
                {
                    float x0 = s.outlineFullWidth ? full.x : inner.x - 3f + s.rowRadius;
                    float x1 = s.outlineFullWidth ? full.xMax : inner.xMax - s.rowRadius;
                    EHDraw.HLine(x0, x1, rect.y + 0.5f, line);
                    EHDraw.HLine(x0, x1, rect.yMax - 0.5f, line);
                }
            }
            return under;
        }

        // ------------------------------------------------------------------ tree lines

        static void DrawTreeLines(Transform t, int depth, float baseX, Rect rect, EHSettings s)
        {
            float midY = rect.center.y;
            Transform cur = t;
            for (int c = depth - 1; c >= 0; c--)
            {
                var parent = cur.parent;
                if (parent == null) break;

                float x = baseX + c * Indent - Indent * 0.5f + s.treeLineOffset;
                LineStyle(parent, s, out Color col, out float th);
                bool last = cur.GetSiblingIndex() == parent.childCount - 1;

                if (c == depth - 1)
                {
                    EHDraw.VLine(x, rect.y, last ? midY + th * 0.5f : rect.yMax, col, th);
                    float endX = t.childCount > 0 ? rect.x - Indent + 2f : rect.x - 2f;
                    EHDraw.HLine(x - th * 0.5f, endX, midY, col, th);
                }
                else if (!last)
                {
                    EHDraw.VLine(x, rect.y, rect.yMax, col, th);
                }
                cur = parent;
            }
        }

        static void LineStyle(Transform parent, EHSettings s, out Color col, out float th)
        {
            if (s.branchColoredLines && EHStore.TryGetBranchColor(parent, out var bc, out _, true))
            {
                col = EHDraw.WithAlpha(EHDraw.Opaque(bc), 0.85f);
                th = Mathf.Max(2f, s.treeLineThickness);
            }
            else
            {
                col = s.treeLineColor;
                th = s.treeLineThickness;
            }
        }

        // ------------------------------------------------------------------ widgets

        // ------------------------------------------------------------------ foldout arrows (legacy)
        // The legacy Hierarchy gives no public "is expanded" flag. Rows are painted top to bottom, so a row
        // is unfolded exactly when the next painted row is one of its children; we remember that per object.
        static readonly System.Collections.Generic.Dictionary<GameObject, bool> ExpandedCache =
            new System.Collections.Generic.Dictionary<GameObject, bool>();
        static GameObject _prevRow;
        static float _prevRowY = float.MinValue;

        static void TrackExpanded(GameObject go, Rect rect)
        {
            if (_prevRow != null && rect.y > _prevRowY && _prevRow.transform.childCount > 0)
            {
                bool expanded = go.transform.parent == _prevRow.transform;
                if (!ExpandedCache.TryGetValue(_prevRow, out var old) || old != expanded)
                {
                    ExpandedCache[_prevRow] = expanded;
                    EditorApplication.RepaintHierarchyWindow();
                }
            }
            _prevRow = go;
            _prevRowY = rect.y;
        }

        /// <summary>Covers Unity's gray arrow and draws a readable one (white on dark rows, black on light).</summary>
        static void DrawFoldoutArrow(GameObject go, Rect rect, Color rowBg, EHSettings s)
        {
            var area = new Rect(rect.x - Indent, rect.y, Indent, rect.height);
            // cover only Unity's arrow itself, so outlines, accent bars and colours around it stay visible
            var c0 = area.center;
            EHDraw.Rect(new Rect(c0.x - 5f, c0.y - 6f, 10f, 12f), rowBg);

            ExpandedCache.TryGetValue(go, out var expanded);
            Color col = EHDraw.WithAlpha(s.brightArrows ? EHDraw.ContrastText(rowBg) : EHDraw.DefaultText, 0.95f);
            if (!go.activeInHierarchy) col.a *= 0.5f;
            var c = area.center;
            Vector3[] tri = expanded
                ? new Vector3[] { new Vector3(c.x - 4.5f, c.y - 2.5f), new Vector3(c.x + 4.5f, c.y - 2.5f), new Vector3(c.x, c.y + 3.5f) }
                : new Vector3[] { new Vector3(c.x - 2.5f, c.y - 4.5f), new Vector3(c.x + 3.5f, c.y), new Vector3(c.x - 2.5f, c.y + 4.5f) };

            var prev = Handles.color;
            Handles.color = col;
            if (s.arrowBadges && expanded)
                Handles.DrawAAPolyLine(2f, tri[0], tri[1], tri[2], tri[0]);
            else
                Handles.DrawAAConvexPolygon(tri);
            Handles.color = prev;
        }

        /// <summary>All descendants (cached until the hierarchy changes).</summary>
        internal static int TotalDescendants(Transform t)
        {
            if (TotalCache.TryGetValue(t, out var n)) return n;
            n = 0;
            for (int i = 0; i < t.childCount; i++) n += 1 + TotalDescendants(t.GetChild(i));
            TotalCache[t] = n;
            return n;
        }

        /// <summary>
        /// Badge text for the child-count mode. <paramref name="expanded"/> is null when unknown
        /// (legacy Hierarchy), in which case Smart shows the total.
        /// </summary>
        internal static string CountText(Transform t, bool? expanded, EHSettings s, out string tooltip)
        {
            int direct = t.childCount;
            int total = TotalDescendants(t);
            tooltip = EHCache.CountTooltip(direct, total);
            string both = direct == total ? EHCache.Number(direct) : EHCache.Pair(direct, total);
            switch (s.childCountMode)
            {
                case ChildCountMode.Direct: return EHCache.Number(direct);
                case ChildCountMode.Total: return EHCache.Number(total);
                case ChildCountMode.Both: return both;
                default: return EHCache.Number(expanded.HasValue && expanded.Value ? direct : total);
            }
        }

        /// <summary>Own color, or the inherited branch color, of a row (used by Accent badges).</summary>
        internal static Color? RowColor(Transform t)
        {
            return EHStore.TryGetBranchColor(t, out var c, out _, true) ? EHDraw.Opaque(c) : (Color?)null;
        }

        /// <summary>Badge background (may be translucent) and readable text color.</summary>
        internal static void BadgeColors(Color under, Color? rowColor, EHSettings s, out Color bg, out Color text)
        {
            switch (s.badgeMode)
            {
                case WidgetColorMode.Subtle:
                    bg = EHDraw.Luminance(under) > 0.35f ? new Color(0f, 0f, 0f, 0.18f) : new Color(1f, 1f, 1f, 0.12f);
                    break;
                case WidgetColorMode.Accent when rowColor.HasValue:
                    bg = AccentAgainst(rowColor.Value, under);
                    break;
                default:
                    bg = EHDraw.Opaque(s.badgeColor);
                    break;
            }
            text = EHDraw.ContrastText(EHDraw.Over(bg, under));
        }

        /// <summary>The accent color, shifted lighter/darker if it would blend into the row behind it.</summary>
        internal static Color AccentAgainst(Color accent, Color under)
        {
            return EHDraw.Contrast(accent, under) < 1.6f
                ? (EHDraw.Luminance(accent) > 0.3f ? EHDraw.Darken(accent, 0.45f) : EHDraw.Lighten(accent, 0.35f))
                : accent;
        }

        /// <summary>"On" color of the active toggle, its "off" color, and the knob/check color drawn on top.</summary>
        internal static void ToggleColors(Color under, Color? rowColor, EHSettings s, bool on,
            out Color onCol, out Color offCol, out Color markCol)
        {
            switch (s.activeToggleColorMode)
            {
                case WidgetColorMode.Subtle:
                    onCol = EditorGUIUtility.isProSkin ? new Color(0.86f, 0.86f, 0.88f, 0.85f) : new Color(0.18f, 0.18f, 0.2f, 0.8f);
                    break;
                case WidgetColorMode.Accent when rowColor.HasValue:
                    onCol = AccentAgainst(rowColor.Value, under);
                    break;
                default:
                    onCol = EHDraw.Opaque(s.toggleOnColor);
                    break;
            }
            offCol = EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.22f) : new Color(0f, 0f, 0f, 0.25f);
            markCol = on ? EHDraw.ContrastText(EHDraw.Over(onCol, under)) : Color.white;
        }

        static void DrawBadge(Rect r, string text, Color under, Color? rowColor, EHSettings s)
        {
            BadgeColors(under, rowColor, s, out var bg, out var tc);
            EHDraw.Rounded(r, bg, r.height * 0.5f);
            EHDraw.Text(r, text, EHDraw.Mini(true), tc, TextAnchor.MiddleCenter);
        }

        static void DrawTagLayer(GameObject go, EHSettings s, Rect tagR, Rect layR, string tagText, string layText, Color under)
        {
            string tag = EHCache.Tag(go);
            bool tagDef = EHTagLayer.IsDefaultTag(tag), layDef = EHTagLayer.IsDefaultLayer(go.layer);
            EHTagLayer.ChipColors(tagDef, EHTagLayer.TagColor(tag), under, out var tagBg, out var tagFg);
            EHTagLayer.ChipColors(layDef, EHTagLayer.LayerColor(go.layer), under, out var layBg, out var layFg);

            if (s.tagLayerStyle == TagLayerStyle.Combined)
            {
                float r = tagR.height * 0.5f;
                EHDraw.RoundedCorners(tagR, tagBg, new Vector4(r, 0f, 0f, r));
                EHDraw.RoundedCorners(layR, layBg, new Vector4(0f, r, r, 0f));
                EHDraw.Text(tagR, tagText, EHDraw.Chip(), tagFg, TextAnchor.MiddleCenter);
                EHDraw.Text(layR, layText, EHDraw.Chip(), layFg, TextAnchor.MiddleCenter);
            }
            else
            {
                EHDraw.Rounded(tagR, tagBg, 7f);
                EHDraw.Rounded(layR, layBg, 7f);
                EHDraw.Text(tagR, "T", EHDraw.Mini(true), tagFg, TextAnchor.MiddleCenter);
                EHDraw.Text(layR, "L", EHDraw.Mini(true), layFg, TextAnchor.MiddleCenter);
            }
        }

        static void DrawActiveToggle(GameObject go, Rect r, EHSettings s, Color under, Color? rowColor)
        {
            var e = Event.current;
            bool on = go.activeSelf;

            if (e.type == EventType.Repaint)
            {
                ToggleColors(under, rowColor, s, on, out var onCol, out var offCol, out var markCol);
                if (on && !go.activeInHierarchy) onCol = EHDraw.WithAlpha(onCol, onCol.a * 0.45f);

                switch (s.activeToggleStyle)
                {
                    case ActiveToggleStyle.Switch:
                    {
                        var track = new Rect(r.center.x - 11f, r.center.y - 6f, 22f, 12f);
                        EHDraw.Rounded(track, on ? onCol : offCol, 6f);
                        var knob = new Rect(on ? track.xMax - 10f : track.x + 2f, track.y + 2f, 8f, 8f);
                        EHDraw.Rounded(knob, markCol, 4f);
                        break;
                    }
                    case ActiveToggleStyle.Dot:
                    {
                        var dot = new Rect(r.center.x - 4.5f, r.center.y - 4.5f, 9f, 9f);
                        if (on) EHDraw.Rounded(dot, onCol, 4.5f);
                        else EHDraw.RoundedBorder(dot, offCol, 4.5f, 1.5f);
                        break;
                    }
                    default: // Checkbox
                    {
                        var box = new Rect(r.center.x - 6f, r.center.y - 6f, 12f, 12f);
                        if (on)
                        {
                            EHDraw.Rounded(box, onCol, 3f);
                            EHDraw.Text(new Rect(box.x, box.y - 1f, box.width, box.height), "✓", EHDraw.Mini(true), markCol, TextAnchor.MiddleCenter);
                        }
                        else EHDraw.RoundedBorder(box, offCol, 3f, 1f);
                        break;
                    }
                }
            }

            if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
            {
                SetActive(go, !on);
                e.Use();
            }
        }

        internal static void SetActive(GameObject go, bool value)
        {
            var targets = Selection.Contains(go) ? Selection.gameObjects : new[] { go };
            Undo.RecordObjects(targets, value ? "Activate GameObject" : "Deactivate GameObject");
            foreach (var g in targets) g.SetActive(value);
        }

        // ------------------------------------------------------------------ helpers

        static int Depth(Transform t)
        {
            int d = 0;
            while (t.parent != null) { t = t.parent; d++; }
            return d;
        }

        static bool HierarchyFocused()
        {
            var f = EditorWindow.focusedWindow;
            return f != null && f.GetType().Name == "SceneHierarchyWindow";
        }
    }
}
