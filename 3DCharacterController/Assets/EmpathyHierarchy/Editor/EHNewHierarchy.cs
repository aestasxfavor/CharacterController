#if UNITY_6000_5_OR_NEWER
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.Reflection;
using Unity.Hierarchy;
using Unity.Hierarchy.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace EmpathyHierarchy
{
    /// <summary>
    /// Renderer for the new (UI Toolkit) Hierarchy window (6.5+, default since 6.6; its API is internal in 6.3/6.4).
    /// Hooks HierarchyWindow.BindViewItem / UnbindViewItem and decorates each recycled row element.
    /// </summary>
    [InitializeOnLoad]
    static class EHNewHierarchy
    {
        internal static readonly HashSet<HierarchyViewItem> Bound = new HashSet<HierarchyViewItem>();
        internal static float IndentWidth = 15f;
        static bool _refreshQueued;

        static EHNewHierarchy()
        {
            try
            {
                var f = typeof(HierarchyViewItem).GetField("k_IndentWidth", BindingFlags.NonPublic | BindingFlags.Static);
                if (f != null && f.GetValue(null) is float w && w > 0f) IndentWidth = w;
            }
            catch { /* keep default */ }

            HierarchyWindow.BindViewItem -= OnBind;
            HierarchyWindow.BindViewItem += OnBind;
            HierarchyWindow.UnbindViewItem -= OnUnbind;
            HierarchyWindow.UnbindViewItem += OnUnbind;

            EHStore.Changed += QueueRefresh;
            EHBookmarks.Changed += QueueRefresh;
            Selection.selectionChanged += QueueRefresh;
            EditorApplication.hierarchyChanged += QueueRefresh;
            EditorApplication.delayCall += BindExisting;
        }

        static readonly HashSet<HierarchyView> HookedViews = new HashSet<HierarchyView>();

        static void HookView(HierarchyView view)
        {
            // fold / unfold changes the Smart child count and the arrow buttons
            if (view != null && HookedViews.Add(view)) view.FlagsChanged += (v, flags) => QueueRefresh();
        }

        internal static bool IsExpanded(HierarchyViewItem item)
        {
            try { return item.View != null && item.View.IsExpanded(item.Node); }
            catch { return false; }
        }

        /// <summary>Folds every GameObject in every new Hierarchy window; scene headers stay open.</summary>
        internal static void CollapseAll()
        {
            foreach (var w in Resources.FindObjectsOfTypeAll<HierarchyWindow>())
            {
                var view = w.View;
                if (view == null) continue;
                view.CollapseAll();
                var scenes = view.Source != null ? HierarchyExtensions.GetNodeTypeHandler<HierarchySceneHandler>(view.Source) : null;
                if (scenes == null) continue;
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var sc = SceneManager.GetSceneAt(i);
                    if (!sc.isLoaded) continue;
                    var node = scenes.GetOrCreateNode(sc);
                    view.Expand(node);
                }
            }
        }

        static void OnBind(HierarchyWindow window, HierarchyView view, HierarchyViewItem item)
        {
            EHSettings.NoteHierarchy(true);
            HookView(view);
            Bound.Add(item);
            Apply(item);
        }

        static void OnUnbind(HierarchyWindow window, HierarchyView view, HierarchyViewItem item)
        {
            Bound.Remove(item);
            item.Q<EHRowDecor>()?.Unbind();
            EHSceneButton.Detach(item);
        }

        /// <summary>Rows bound before this script loaded (e.g. after a domain reload).</summary>
        static void BindExisting()
        {
            foreach (var w in Resources.FindObjectsOfTypeAll<HierarchyWindow>())
            {
                var view = w.View;
                if (view == null) continue;
                HookView(view);
                view.Query<HierarchyViewItem>().ForEach(i =>
                {
                    Bound.Add(i);
                    Apply(i);
                });
            }
        }

        // cached delegates: no closure allocation each time a refresh is queued
        static readonly EditorApplication.CallbackFunction RefreshCallback = RefreshAll;
        static readonly System.Predicate<HierarchyViewItem> IsDetached = i => i == null || i.panel == null;

        static void QueueRefresh()
        {
            if (_refreshQueued) return;
            _refreshQueued = true;
            EditorApplication.delayCall += RefreshCallback;
        }

        static void RefreshAll()
        {
            _refreshQueued = false;
            Bound.RemoveWhere(IsDetached);
            foreach (var i in Bound) Apply(i);
        }

        static void Apply(HierarchyViewItem item)
        {
            // scene header rows get the ▾ scene selector; rows are recycled, so detach it everywhere else
            if (item.Handler is HierarchySceneHandler && EHSettings.instance.enabled && EHSettings.instance.sceneSelector)
                EHSceneButton.Attach(item);
            else
                EHSceneButton.Detach(item);

            var go = GetGameObject(item);
            var decor = item.Q<EHRowDecor>();
            if (go == null || !EHSettings.instance.enabled)
            {
                decor?.Unbind();
                return;
            }
            if (decor == null)
            {
                decor = new EHRowDecor(item);
                item.Insert(0, decor);
            }
            decor.Bind(go);
        }

        internal static GameObject GetGameObject(HierarchyViewItem item)
        {
            return item.Handler is HierarchyGameObjectHandler handler ? handler.GetGameObject(item.Node) : null;
        }

        internal static bool HierarchyFocused() => EditorWindow.focusedWindow is HierarchyWindow;

        // Real indent per Hierarchy window, measured from a parent row and its child row in that window.
        static readonly Dictionary<IPanel, float> PanelIndent = new Dictionary<IPanel, float>();

        internal static float IndentFor(IPanel panel) =>
            panel != null && PanelIndent.TryGetValue(panel, out var w) ? w : IndentWidth;

        internal static void Measure(HierarchyViewItem item, GameObject go)
        {
            var panel = item.panel;
            if (panel == null || go.transform.parent == null) return;
            var parentGo = go.transform.parent.gameObject;
            foreach (var other in Bound)
            {
                if (other == null || other.panel != panel) continue;
                if (GetGameObject(other) != parentGo) continue;
                float w = item.Icon.worldBound.x - other.Icon.worldBound.x;
                if (float.IsNaN(w) || w < 6f || w > 40f) return;
                if (!PanelIndent.TryGetValue(panel, out var old) || Mathf.Abs(old - w) > 0.25f)
                {
                    PanelIndent[panel] = w;
                    foreach (var i in Bound)
                        if (i != null && i.panel == panel) i.Q<EHRowDecor>()?.MarkDirtyRepaint();
                }
                return;
            }
        }
    }

    /// <summary>
    /// Absolute layer placed behind a row's content: colored pill, separator, tree lines and outlines.
    /// Also owns the right-side badge / active toggle and restores everything on unbind.
    /// </summary>
    class EHRowDecor : VisualElement
    {
        readonly HierarchyViewItem _item;
        GameObject _go;

        readonly VisualElement _pill, _accent, _sep, _lineL, _lineR, _outline, _arrowBg;
        readonly Label _sepLabel, _badge;
        readonly EHActiveWidget _toggle;
        readonly EHTagLayerWidget _tagLayer = new EHTagLayerWidget();
        readonly Label _star = MakeStar();

        const int StarSize = 15;

        static Label MakeStar()
        {
            // right after the object's name, clear yellow with a black outline on any row color
            var l = new Label("★") { pickingMode = PickingMode.Ignore };
            l.style.position = Position.Absolute;
            l.style.fontSize = StarSize;
            l.style.color = EHTheme.Star;
            l.style.unityTextOutlineWidth = 1f;
            l.style.unityTextOutlineColor = Color.black;
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.width = StarSize + 2; l.style.height = StarSize + 4;
            l.style.marginLeft = 0; l.style.marginRight = 0; l.style.marginTop = 0; l.style.marginBottom = 0;
            l.style.paddingLeft = 0; l.style.paddingRight = 0; l.style.paddingTop = 0; l.style.paddingBottom = 0;
            return l;
        }

        bool _starHooked;
        static GUIStyle _measure;

        /// <summary>
        /// Width of the name's text. UI Toolkit's own measuring can report 0 before the row's text has settled,
        /// so the IMGUI label (same editor font) at the label's size and weight is used too, and the larger wins.
        /// </summary>
        static float NameWidth(Label label)
        {
            if (_measure == null) _measure = new GUIStyle(EditorStyles.label) { padding = new RectOffset(0, 0, 0, 0) };
            var rs = label.resolvedStyle;
            _measure.fontSize = Mathf.RoundToInt(rs.fontSize);
            var w = rs.unityFontStyleAndWeight;
            _measure.fontStyle = w == FontStyle.Bold || w == FontStyle.BoldAndItalic ? FontStyle.Bold : FontStyle.Normal;
            float uitk = label.MeasureTextSize(label.text, 0, MeasureMode.Undefined, 0, MeasureMode.Undefined).x;
            return uitk > 0f ? uitk : _measure.CalcSize(new GUIContent(label.text)).x; // UI Toolkit's own when it has one
        }

        /// <summary>Star on the row (absolute), just after the name's text, never over our right-side widgets.</summary>
        void PlaceStar()
        {
            if (_item.Name != null && _item.Name.panel != null && float.IsNaN(_item.Name.layout.width)) return;
            var nameLabel = _item.Name;
            if (_star.parent != _item || nameLabel == null || nameLabel.panel == null) return;
            var nl = nameLabel.layout;
            if (float.IsNaN(nl.width)) return;
            var p = nameLabel.ChangeCoordinatesTo(_item, new Vector2(
                nameLabel.resolvedStyle.paddingLeft + NameWidth(nameLabel) + 2f, (nl.height - (StarSize + 4)) * 0.5f));
            // only our own widgets count: Unity's right container also holds a stretching spacer right after the name
            float limit = _item.layout.width - 16f;
            foreach (var w in new VisualElement[] { _badge, _tagLayer, _toggle })
                if (w.parent != null && !float.IsNaN(w.layout.width) && w.layout.width > 0f)
                    limit = Mathf.Min(limit, w.ChangeCoordinatesTo(_item, Vector2.zero).x - (StarSize + 3));
            _star.style.left = Mathf.Round(Mathf.Min(p.x, limit));
            _star.style.top = Mathf.Round(p.y);
            _star.style.visibility = Visibility.Visible;
        }

        bool _iconOverridden;
        StyleBackground _originalIcon;
        Color _lineColor;
        bool _extend;

        const float ArrowSize = 14f; // drawing area for our triangle (no circle)
        Color _arrowColor = Color.white;
        bool _arrowExpanded, _arrowHollow;

        public EHRowDecor(HierarchyViewItem item)
        {
            _item = item;
            name = "empathy-hierarchy-decor";
            pickingMode = PickingMode.Ignore;
            Absolute(this, 0, 0, 0, 0);

            _pill = new VisualElement { pickingMode = PickingMode.Ignore };
            _pill.style.position = Position.Absolute;
            _pill.style.top = PixelLine; _pill.style.bottom = PixelLine; _pill.style.right = 2;
            _accent = new VisualElement { pickingMode = PickingMode.Ignore };
            Absolute(_accent, 0, 0, null, 0);
            _accent.style.width = 3;
            _pill.Add(_accent);
            Add(_pill);

            _sep = new VisualElement { pickingMode = PickingMode.Ignore };
            _sep.style.position = Position.Absolute;
            // same right edge (and pixel insets) as the colored pill of normal rows, so the bars line up
            _sep.style.top = PixelLine; _sep.style.bottom = PixelLine; _sep.style.right = 2;
            _sep.style.flexDirection = FlexDirection.Row;
            _sep.style.alignItems = Align.Center;
            _lineL = new VisualElement { pickingMode = PickingMode.Ignore };
            _lineR = new VisualElement { pickingMode = PickingMode.Ignore };
            _lineL.style.height = 1; _lineR.style.height = 1;
            _sepLabel = new Label { pickingMode = PickingMode.Ignore };
            _sepLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _sepLabel.style.marginLeft = 6; _sepLabel.style.marginRight = 6;
            _sepLabel.style.paddingLeft = 0; _sepLabel.style.paddingRight = 0;
            _sep.Add(_lineL); _sep.Add(_sepLabel); _sep.Add(_lineR);
            Add(_sep);

            // round button behind the foldout arrow (above the pill, below the arrow itself)
            _arrowBg = new VisualElement { pickingMode = PickingMode.Ignore };
            _arrowBg.style.position = Position.Absolute;
            _arrowBg.style.width = ArrowSize; _arrowBg.style.height = ArrowSize;
            Radius(_arrowBg, ArrowSize * 0.5f);
            _arrowBg.generateVisualContent += DrawArrow;
            Add(_arrowBg);

            // top/bottom outline drawn above the pill; bottom inset 1px so the next row can't cover it
            _outline = new VisualElement { pickingMode = PickingMode.Ignore };
            // on exactly the same device pixels as the pill border, so top and bottom read equally thick
            Absolute(_outline, 0, PixelLine, 0, PixelLine);
            _outline.style.borderTopWidth = PixelLine;
            _outline.style.borderBottomWidth = PixelLine;
            Add(_outline);

            _badge = new Label(); // pickable so its tooltip (children / all descendants) shows
            _badge.style.unityFontStyleAndWeight = FontStyle.Bold;
            _badge.style.fontSize = 10;
            _badge.style.unityTextAlign = TextAnchor.MiddleCenter;
            _badge.style.minWidth = 18;
            _badge.style.height = 14;
            _badge.style.alignSelf = Align.Center;
            _badge.style.paddingLeft = 5; _badge.style.paddingRight = 5;
            _badge.style.paddingTop = 0; _badge.style.paddingBottom = 0;
            _badge.style.marginRight = 4;
            Radius(_badge, 7);

            _toggle = new EHActiveWidget(value =>
            {
                if (_go != null) EHHierarchyGUI.SetActive(_go, value);
            });

            generateVisualContent += Draw;
            item.Icon.RegisterCallback<GeometryChangedEvent>(_ => UpdateGeometry());
        }

        // ------------------------------------------------------------------ bind / unbind

        public void Bind(GameObject go)
        {
            _go = go;
            style.display = DisplayStyle.Flex;

            var s = EHSettings.instance;
            var st = EHStore.Get(go);
            var t = go.transform;
            int depth = Depth(t);
            bool selected = Selection.Contains(go);
            Color baseBg = selected
                ? (EHNewHierarchy.HierarchyFocused() ? EHDraw.SelectionFocused : EHDraw.SelectionUnfocused)
                : EHDraw.WindowBg;
            bool isSep = EHHierarchyGUI.IsSeparator(EHCache.Name(go), s);
            bool own = st != null && st.hasColor && !st.tintOnly;
            bool active = go.activeInHierarchy;
            float r = s.rowRadius;

            var nameLabel = _item.Name;
            var icon = _item.Icon;
            nameLabel.style.color = StyleKeyword.Null;
            nameLabel.style.unityFontStyleAndWeight = StyleKeyword.Null;
            nameLabel.style.visibility = StyleKeyword.Null;
            icon.style.visibility = StyleKeyword.Null;

            Color under = baseBg;
            _pill.style.display = DisplayStyle.None;
            _sep.style.display = DisplayStyle.None;
            _outline.style.display = DisplayStyle.None;

            _extend = s.extendRootRows && (isSep || (depth == 0 && own));
            Color? arrowColor = null;

            if (isSep)
            {
                under = BindSeparator(go, st, s, baseBg, selected);
                // same color as the separator label (set inline by BindSeparator)
                if (st != null && st.hasColor && s.arrowTint != ArrowTintMode.Off) arrowColor = _sepLabel.style.color.value;
                nameLabel.style.visibility = Visibility.Hidden;
                icon.style.visibility = Visibility.Hidden;
                RestoreIcon();
            }
            else
            {
                Color textColor = default;
                bool setText = false;

                if (own)
                {
                    Color c = EHDraw.Opaque(st.Color1);
                    Color fa = EHHierarchyGUI.FillColor(c, st.fill, baseBg);
                    Color fb = st.gradient == GradientMode.TwoColor
                        ? EHHierarchyGUI.FillColor(EHDraw.Opaque(st.Color2), st.fill, baseBg)
                        : baseBg;
                    if (selected) { fa = Color.Lerp(fa, baseBg, 0.35f); fb = Color.Lerp(fb, baseBg, 0.35f); }

                    Fill(_pill, fa, fb, st.gradient != GradientMode.None);
                    Radius(_pill, r);
                    Color border = selected ? EHDraw.Lighten(EHDraw.SelectionFocused, 0.35f)
                        : st.fill == FillMode.Solid ? Color.clear
                        : EHDraw.WithAlpha(c, st.fill == FillMode.Outline ? 0.9f : 0.45f);
                    // whole device pixels: at 125% / 150% display scaling a 1pt line lands between pixels and the
                    // top and bottom edges come out with different thickness
                    float px = PixelLine;
                    _pill.style.top = px; _pill.style.bottom = px;
                    Border(_pill, border, border.a > 0f ? px : 0f);
                    _accent.style.display = s.accentBar ? DisplayStyle.Flex : DisplayStyle.None;
                    _accent.style.backgroundColor = st.fill == FillMode.Solid ? EHDraw.Lighten(c, 0.45f) : c;
                    _accent.style.width = 3;
                    RadiusLeft(_accent, Mathf.Min(r, 3f));
                    _pill.style.display = DisplayStyle.Flex;

                    textColor = st.fill == FillMode.Solid ? EHDraw.ContrastText(fa) : EHDraw.TintedText(c, fa);
                    setText = true;
                    // arrow sits inside the pill when extended, otherwise on the plain row background
                    if (depth == 0 && s.arrowTint != ArrowTintMode.Off)
                        arrowColor = _extend ? textColor : EHDraw.TintedText(c, baseBg);
                    else if (s.arrowTint == ArrowTintMode.AllColored)
                        arrowColor = EHDraw.TintedText(c, baseBg);
                    under = st.gradient == GradientMode.None ? fa : fb;
                }
                else if (s.childColors && EHStore.TryGetBranchColor(t, out var branch, out var dist, false))
                {
                    float a = s.childTintAlpha * Mathf.Pow(s.childTintFade, Mathf.Max(0, dist - 1));
                    Color tint = EHDraw.Over(EHDraw.WithAlpha(branch, a), baseBg);
                    Fill(_pill, EHDraw.WithAlpha(branch, a), Color.clear, false);
                    Radius(_pill, r);
                    Border(_pill, Color.clear, 0f);
                    _accent.style.display = s.accentBar ? DisplayStyle.Flex : DisplayStyle.None;
                    _accent.style.backgroundColor = EHDraw.WithAlpha(branch, 0.7f);
                    _accent.style.width = 2;
                    RadiusLeft(_accent, Mathf.Min(r, 2f));
                    _pill.style.display = DisplayStyle.Flex;
                    under = tint;
                }

                if (setText)
                {
                    if (!active) textColor.a *= 0.5f;
                    nameLabel.style.color = textColor;
                }
                if (arrowColor.HasValue && !active) arrowColor = EHDraw.WithAlpha(arrowColor.Value, 0.5f);
                bool bold = (st != null && st.bold) || (s.boldRootObjects && depth == 0);
                if (bold) nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

                if (EHIcons.HasCustomIcon(st, s.componentAwareIcons) && EHIcons.Resolve(go, st, s.componentAwareIcons) is Texture2D tex)
                {
                    if (!_iconOverridden) { _originalIcon = icon.style.backgroundImage; _iconOverridden = true; }
                    icon.style.backgroundImage = new StyleBackground(tex);
                    if (st != null && st.iconDarken > 0f) icon.style.unityBackgroundImageTintColor = st.IconTint;
                    else icon.style.unityBackgroundImageTintColor = StyleKeyword.Null;
                }
                else RestoreIcon();
            }

            // ---- foldout arrow: round button (A/B) and brighter arrow (C)
            // Unity's arrow image is gray, and a tint can only darken it, so for these rows we hide it
            // (it stays clickable) and draw our own triangle centered in the round button.
            bool hasKids = t.childCount > 0;
            bool expanded = hasKids && EHNewHierarchy.IsExpanded(_item);
            Color arrowUnder = _extend ? under : baseBg;
            bool customArrow = hasKids && (s.arrowBadges || s.brightArrows);

            if (customArrow)
            {
                bool lightBehind = EHDraw.Luminance(arrowUnder) > 0.35f;
                Color tone = lightBehind ? Color.black : Color.white;
                Color tri = arrowColor ?? (s.brightArrows
                    ? EHDraw.WithAlpha(EHDraw.ContrastText(arrowUnder), 0.95f)
                    : EHDraw.WithAlpha(tone, 0.55f));
                if (!active) tri = EHDraw.WithAlpha(tri, tri.a * 0.5f);
                _arrowColor = tri;
                _arrowExpanded = expanded;
                SetArrowTint(Color.clear);

                // no circle any more: the triangle itself shows the state
                _arrowBg.style.backgroundColor = Color.clear;
                Border(_arrowBg, Color.clear, 0f);
                _arrowHollow = s.arrowBadges && expanded;
                _arrowBg.style.display = DisplayStyle.Flex;
                _arrowBg.MarkDirtyRepaint();
            }
            else
            {
                SetArrowTint(arrowColor);
                _arrowBg.style.display = DisplayStyle.None;
            }

            if (!isSep && depth == 0 &&
                (s.rootOutline == RootOutlineMode.AllRoots || (s.rootOutline == RootOutlineMode.ColoredRoots && own)))
            {
                ShowOutline(own ? EHDraw.WithAlpha(EHDraw.Opaque(st.Color1), 0.9f) : s.outlineColor);
            }

            // ---- right side widgets, always in this order: star, child count, tag/layer, active
            var right = _item.RightCustomContainer;
            _tagLayer.RemoveFromHierarchy();
            _badge.RemoveFromHierarchy();
            _toggle.RemoveFromHierarchy();
            if (!isSep && s.navBar && s.navStarOnRows && EHBookmarks.Contains(go) && _item.Name != null)
            {
                if (_star.parent != _item)
                {
                    _star.style.visibility = Visibility.Hidden; // shown once placed (no jump from a first guess)
                    _item.Add(_star);
                }
                if (!_starHooked)
                {
                    _starHooked = true;
                    _item.Name.RegisterCallback<GeometryChangedEvent>(_ => PlaceStar());
                    _item.RegisterCallback<GeometryChangedEvent>(_ => PlaceStar());
                }
                // placed once the name's text / bold and the widgets have settled (placing now would use stale sizes)
                schedule.Execute(PlaceStar);
            }
            else _star.RemoveFromHierarchy();
            if (right != null)
            {
                if (s.childCountBadge && t.childCount > 0)
                {
                    _badge.text = EHHierarchyGUI.CountText(t, expanded, s, out var countTip);
                    _badge.tooltip = countTip;
                    EHHierarchyGUI.BadgeColors(under, EHHierarchyGUI.RowColor(t), s, out var bg, out var tc);
                    _badge.style.backgroundColor = bg;
                    _badge.style.color = tc;
                    right.Add(_badge);
                }
                if (!isSep && EHTagLayer.ShouldShow(go, s))
                {
                    _tagLayer.Bind(go, s, under);
                    right.Add(_tagLayer);
                }
                if (s.activeToggle && !isSep) // separators are labels, not objects to switch
                {
                    _toggle.SetState(go.activeSelf, go.activeInHierarchy, s, under, EHHierarchyGUI.RowColor(t));
                    right.Add(_toggle);
                }
            }

            UpdateGeometry();
            schedule.Execute(UpdateGeometry);
            MarkDirtyRepaint();
        }

        VisualElement Arrow => _item.Toggle?.Q("unity-checkmark");

        void SetArrowTint(Color? c)
        {
            var arrow = Arrow;
            if (arrow == null) return;
            if (c.HasValue) arrow.style.unityBackgroundImageTintColor = c.Value;
            else arrow.style.unityBackgroundImageTintColor = StyleKeyword.Null;
        }

        void ShowOutline(Color c)
        {
            _outline.style.borderTopColor = c;
            _outline.style.borderBottomColor = c;
            _outline.style.display = DisplayStyle.Flex;
        }

        Color BindSeparator(GameObject go, ObjectStyle st, EHSettings s, Color baseBg, bool selected)
        {
            string label = EHCache.SeparatorLabel(go);
            var align = st != null && st.align != SeparatorAlign.Default ? st.align : s.separatorAlign;
            if (align == SeparatorAlign.Default) align = SeparatorAlign.Center;

            bool own = st != null && st.hasColor && !st.tintOnly;
            Color c = own ? EHDraw.Opaque(st.Color1) : EHDraw.Opaque(s.outlineColor);
            _lineColor = own ? EHDraw.WithAlpha(c, 0.9f) : s.outlineColor;
            bool solid = own && st.fill == FillMode.Solid;

            _sepLabel.text = label;
            _sepLabel.style.display = label.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            SetLine(_lineL, align == SeparatorAlign.Left);
            SetLine(_lineR, align == SeparatorAlign.Right);

            Color under = baseBg;
            if (solid)
            {
                Color fa = selected ? Color.Lerp(c, baseBg, 0.35f) : c;
                Color fb = st.gradient == GradientMode.TwoColor ? EHDraw.Opaque(st.Color2)
                    : st.gradient == GradientMode.Fade ? baseBg : fa;
                Fill(_sep, fa, fb, st.gradient != GradientMode.None);
                Radius(_sep, s.rowRadius);
                _lineL.style.backgroundColor = Color.clear;
                _lineR.style.backgroundColor = Color.clear;
                _sepLabel.style.color = EHDraw.ContrastText(fa);
                under = fb;
            }
            else
            {
                if (own && st.fill == FillMode.Soft)
                {
                    under = Color.Lerp(baseBg, c, EditorGUIUtility.isProSkin ? 0.30f : 0.38f);
                    Fill(_sep, under, Color.clear, false);
                }
                else Fill(_sep, Color.clear, Color.clear, false);
                Radius(_sep, s.rowRadius);
                _lineL.style.backgroundColor = _lineColor;
                _lineR.style.backgroundColor = _lineColor;
                _sepLabel.style.color = own ? EHDraw.TintedText(c, under) : EHDraw.ContrastText(under);
            }
            bool outlineBox = own && st.fill == FillMode.Outline;
            Border(_sep, outlineBox ? EHDraw.WithAlpha(c, 0.9f) : Color.clear, outlineBox ? 1f : 0f);
            _sep.style.display = DisplayStyle.Flex;
            if (s.separatorOutline && label.Length > 0) ShowOutline(_lineColor);
            return under;
        }

        static void SetLine(VisualElement line, bool shortSide)
        {
            if (shortSide)
            {
                line.style.flexGrow = 0f;
                line.style.width = 8f;
            }
            else
            {
                line.style.flexGrow = 1f;
                line.style.width = StyleKeyword.Auto;
            }
        }

        public void Unbind()
        {
            if (_item != null)
            {
                _item.Name.style.color = StyleKeyword.Null;
                _item.Name.style.unityFontStyleAndWeight = StyleKeyword.Null;
                _item.Name.style.visibility = StyleKeyword.Null;
                SetArrowTint(null);
                _item.Icon.style.visibility = StyleKeyword.Null;
                RestoreIcon();
            }
            _badge.RemoveFromHierarchy();
            _toggle.RemoveFromHierarchy();
            _tagLayer.RemoveFromHierarchy();
            _star.RemoveFromHierarchy();
            _go = null;
            style.display = DisplayStyle.None;
        }

        void RestoreIcon()
        {
            if (!_iconOverridden) return;
            _item.Icon.style.backgroundImage = _originalIcon;
            _item.Icon.style.unityBackgroundImageTintColor = StyleKeyword.Null;
            _iconOverridden = false;
        }

        void UpdateGeometry()
        {
            if (_go == null || panel == null) return;
            var ic = this.WorldToLocal(_item.Icon.worldBound);
            if (float.IsNaN(ic.x)) return;
            EHNewHierarchy.Measure(_item, _go);

            float left = ic.x - 3f;
            if (_extend)
            {
                var tg = this.WorldToLocal(_item.Toggle.worldBound);
                left = (tg.width > 1f && !float.IsNaN(tg.x)) ? tg.x - 1f : ic.x - EHNewHierarchy.IndentFor(panel) - 1f;
            }
            _pill.style.left = left;
            _sep.style.left = left;

            // center on Unity's own arrow glyph (falls back to the toggle box)
            var glyph = Arrow;
            var gr = glyph != null ? this.WorldToLocal(glyph.worldBound) : Rect.zero;
            if (gr.width < 1f || float.IsNaN(gr.x)) gr = this.WorldToLocal(_item.Toggle.worldBound);
            if (gr.width > 1f && !float.IsNaN(gr.x))
            {
                _arrowBg.style.left = Mathf.Round(gr.center.x - ArrowSize * 0.5f);
                _arrowBg.style.top = Mathf.Round(contentRect.height * 0.5f - ArrowSize * 0.5f);
            }
            // separator lines/text still start where the icon would be, so the arrow keeps its space
            _sep.style.paddingLeft = Mathf.Max(0f, ic.x - 2f - left);

            // outline: full width, or only along the straight part of the rounded row
            var s = EHSettings.instance;
            if (s.outlineFullWidth)
            {
                _outline.style.left = 0f;
                _outline.style.right = 0f;
            }
            else
            {
                float inset = Mathf.Min(s.rowRadius, 7f);
                _outline.style.left = left + inset;
                _outline.style.right = 2f + inset;
            }
            MarkDirtyRepaint();
        }

        /// <summary>
        /// Our own foldout triangle, as large as fits the row: ▶ folded, ▼ unfolded.
        /// Filled = folded (children hidden), hollow = unfolded (when "Arrow Badges" is on).
        /// </summary>
        void DrawArrow(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            float c = ArrowSize * 0.5f;
            Vector2 a, b, d;
            if (_arrowExpanded)
            {
                a = new Vector2(c - 5f, c - 3f);
                b = new Vector2(c + 5f, c - 3f);
                d = new Vector2(c, c + 4f);
            }
            else
            {
                a = new Vector2(c - 3f, c - 5f);
                b = new Vector2(c + 4f, c);
                d = new Vector2(c - 3f, c + 5f);
            }
            p.BeginPath();
            p.MoveTo(a);
            p.LineTo(b);
            p.LineTo(d);
            p.ClosePath();
            if (_arrowHollow)
            {
                p.strokeColor = _arrowColor;
                p.lineWidth = 1.5f;
                p.lineJoin = LineJoin.Round;
                p.Stroke();
            }
            else
            {
                p.fillColor = _arrowColor;
                p.Fill();
            }
        }

        // ------------------------------------------------------------------ lines

        void Draw(MeshGenerationContext ctx)
        {
            if (_go == null) return;
            var s = EHSettings.instance;
            var p = ctx.painter2D;
            var rect = contentRect;
            float h = rect.height, mid = h * 0.5f;
            if (h <= 0f) return;

            var t = _go.transform;
            int depth = Depth(t);
            var ic = this.WorldToLocal(_item.Icon.worldBound);
            var tg = this.WorldToLocal(_item.Toggle.worldBound);
            float indent = EHNewHierarchy.IndentFor(panel);
            // center of this row's own foldout arrow; parents' arrows sit one indent further left each
            float ownArrow = (tg.width > 1f && !float.IsNaN(tg.x)) ? tg.center.x : ic.x - indent * 0.5f;
            ownArrow += s.treeLineOffset;

            if (s.treeLines && depth > 0 && !float.IsNaN(ic.x))
            {
                Transform cur = t;
                for (int c = depth - 1; c >= 0; c--)
                {
                    var parent = cur.parent;
                    if (parent == null) break;
                    float x = ownArrow - indent * (depth - c);
                    LineStyle(parent, s, out Color col, out float th);
                    bool last = cur.GetSiblingIndex() == parent.childCount - 1;
                    if (c == depth - 1)
                    {
                        FillRect(p, x - th * 0.5f, 0f, th, (last ? mid + th * 0.5f : h), col);
                        float endX = t.childCount > 0
                            ? ownArrow - 6f
                            : ic.x - 2f;
                        FillRect(p, x - th * 0.5f, mid - th * 0.5f, endX - (x - th * 0.5f), th, col);
                    }
                    else if (!last)
                    {
                        FillRect(p, x - th * 0.5f, 0f, th, h, col);
                    }
                    cur = parent;
                }
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

        static void FillRect(Painter2D p, float x, float y, float w, float h, Color c)
        {
            if (w <= 0f || h <= 0f || c.a <= 0f) return;
            p.fillColor = c;
            p.BeginPath();
            p.MoveTo(new Vector2(x, y));
            p.LineTo(new Vector2(x + w, y));
            p.LineTo(new Vector2(x + w, y + h));
            p.LineTo(new Vector2(x, y + h));
            p.ClosePath();
            p.Fill();
        }

        // ------------------------------------------------------------------ style helpers

        static void Fill(VisualElement e, Color a, Color b, bool gradient)
        {
            if (gradient)
            {
                e.style.backgroundColor = Color.clear;
                e.style.backgroundImage = new StyleBackground(EHDraw.Gradient(EHDraw.Opaque(a), EHDraw.Opaque(b)));
                e.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            }
            else
            {
                e.style.backgroundImage = StyleKeyword.Null;
                e.style.backgroundColor = a;
            }
        }

        static void Radius(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = r; e.style.borderTopRightRadius = r;
            e.style.borderBottomLeftRadius = r; e.style.borderBottomRightRadius = r;
        }

        static void RadiusLeft(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = r; e.style.borderBottomLeftRadius = r;
            e.style.borderTopRightRadius = 0; e.style.borderBottomRightRadius = 0;
        }

        /// <summary>One device pixel (or two on 200%+ screens), in points.</summary>
        static float PixelLine
        {
            get
            {
                float ppp = EditorGUIUtility.pixelsPerPoint;
                return Mathf.Max(1f, Mathf.Round(ppp)) / ppp;
            }
        }

        static void Border(VisualElement e, Color c, float w)
        {
            e.style.borderTopWidth = w; e.style.borderBottomWidth = w;
            e.style.borderLeftWidth = w; e.style.borderRightWidth = w;
            e.style.borderTopColor = c; e.style.borderBottomColor = c;
            e.style.borderLeftColor = c; e.style.borderRightColor = c;
        }

        static void Absolute(VisualElement e, float? left, float? top, float? right, float? bottom)
        {
            e.style.position = Position.Absolute;
            if (left.HasValue) e.style.left = left.Value;
            if (top.HasValue) e.style.top = top.Value;
            if (right.HasValue) e.style.right = right.Value;
            if (bottom.HasValue) e.style.bottom = bottom.Value;
        }

        static int Depth(Transform t)
        {
            int d = 0;
            while (t.parent != null) { t = t.parent; d++; }
            return d;
        }
    }
    /// <summary>Compact active toggle drawn as a switch, dot or checkbox.</summary>
    class EHActiveWidget : VisualElement
    {
        readonly VisualElement _track, _knob;
        readonly Label _check;
        readonly System.Action<bool> _changed;
        bool _value;

        public EHActiveWidget(System.Action<bool> changed)
        {
            _changed = changed;
            style.alignSelf = Align.Center;
            style.justifyContent = Justify.Center;
            style.alignItems = Align.Center;
            style.marginLeft = 2; style.marginRight = 3;
            style.width = 24; style.height = 16;

            _track = new VisualElement { pickingMode = PickingMode.Ignore };
            _knob = new VisualElement { pickingMode = PickingMode.Ignore };
            _knob.style.position = Position.Absolute;
            _check = new Label("✓") { pickingMode = PickingMode.Ignore };
            _check.style.fontSize = 9;
            _check.style.unityFontStyleAndWeight = FontStyle.Bold;
            _check.style.unityTextAlign = TextAnchor.MiddleCenter;
            _check.style.color = Color.white;
            _check.style.paddingLeft = 0; _check.style.paddingRight = 0;
            _check.style.paddingTop = 0; _check.style.paddingBottom = 0;
            _check.style.marginLeft = 0; _check.style.marginRight = 0;
            _check.style.position = Position.Absolute;
            _check.style.left = 0; _check.style.right = 0; _check.style.top = -1; _check.style.bottom = 0;
            _track.Add(_knob);
            _track.Add(_check);
            Add(_track);

            RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                _changed?.Invoke(!_value);
                evt.StopPropagation();
            });
        }

        public void SetState(bool on, bool effective, EHSettings s, Color under, Color? rowColor)
        {
            _value = on;
            EHHierarchyGUI.ToggleColors(under, rowColor, s, on, out var onCol, out var offCol, out var markCol);
            _track.style.opacity = on && !effective ? 0.5f : 1f;
            _knob.style.backgroundColor = markCol;
            _check.style.color = markCol;
            SetBorder(_track, Color.clear, 0f);

            switch (s.activeToggleStyle)
            {
                case ActiveToggleStyle.Switch:
                    Size(_track, 22, 12, 6);
                    _track.style.backgroundColor = on ? onCol : offCol;
                    Size(_knob, 8, 8, 4);
                    _knob.style.top = 2;
                    _knob.style.left = on ? 12 : 2;
                    _knob.style.display = DisplayStyle.Flex;
                    _check.style.display = DisplayStyle.None;
                    break;
                case ActiveToggleStyle.Dot:
                    Size(_track, 9, 9, 4.5f);
                    _track.style.backgroundColor = on ? onCol : Color.clear;
                    if (!on) SetBorder(_track, offCol, 1.5f);
                    _knob.style.display = DisplayStyle.None;
                    _check.style.display = DisplayStyle.None;
                    break;
                default: // Checkbox
                    Size(_track, 12, 12, 3);
                    _track.style.backgroundColor = on ? onCol : Color.clear;
                    if (!on) SetBorder(_track, offCol, 1f);
                    _knob.style.display = DisplayStyle.None;
                    _check.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
                    break;
            }
        }

        static void Size(VisualElement e, float w, float h, float r)
        {
            e.style.width = w; e.style.height = h;
            e.style.borderTopLeftRadius = r; e.style.borderTopRightRadius = r;
            e.style.borderBottomLeftRadius = r; e.style.borderBottomRightRadius = r;
        }

        static void SetBorder(VisualElement e, Color c, float w)
        {
            e.style.borderTopWidth = w; e.style.borderBottomWidth = w;
            e.style.borderLeftWidth = w; e.style.borderRightWidth = w;
            e.style.borderTopColor = c; e.style.borderBottomColor = c;
            e.style.borderLeftColor = c; e.style.borderRightColor = c;
        }
    }
    /// <summary>Tag/layer chip ("TAG/LAY") or T/L circles; click opens the themed picker.</summary>
    class EHTagLayerWidget : VisualElement
    {
        readonly VisualElement _combined, _circles;
        readonly Label _tag, _layer, _t, _l;
        GameObject _go;

        public EHTagLayerWidget()
        {
            style.flexDirection = FlexDirection.Row;
            style.alignSelf = Align.Center;
            style.marginRight = 4;

            _combined = new VisualElement();
            _combined.style.flexDirection = FlexDirection.Row;
            _combined.style.height = 14;
            _combined.style.overflow = Overflow.Hidden;
            SetRadius(_combined, 7);
            _tag = ChipLabel();
            _layer = ChipLabel();
            _combined.Add(_tag);
            _combined.Add(_layer);
            Add(_combined);

            _circles = new VisualElement();
            _circles.style.flexDirection = FlexDirection.Row;
            _t = Circle("T");
            _l = Circle("L");
            _l.style.marginLeft = 2;
            _circles.Add(_t);
            _circles.Add(_l);
            Add(_circles);

            _combined.RegisterCallback<PointerDownEvent>(e => Open(e, EHTagLayerPopup.Mode.Both, _combined));
            _t.RegisterCallback<PointerDownEvent>(e => Open(e, EHTagLayerPopup.Mode.Tag, _t));
            _l.RegisterCallback<PointerDownEvent>(e => Open(e, EHTagLayerPopup.Mode.Layer, _l));
        }

        static Label ChipLabel()
        {
            var l = new Label { pickingMode = PickingMode.Ignore };
            l.style.fontSize = 9;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.paddingLeft = 5; l.style.paddingRight = 5;
            l.style.paddingTop = 0; l.style.paddingBottom = 0;
            l.style.marginLeft = 0; l.style.marginRight = 0; l.style.marginTop = 0; l.style.marginBottom = 0;
            l.style.height = 14;
            l.style.whiteSpace = WhiteSpace.NoWrap;
            l.style.overflow = Overflow.Hidden;
            l.style.textOverflow = TextOverflow.Ellipsis;
            return l;
        }

        static Label Circle(string text)
        {
            var l = new Label(text);
            l.style.width = 14; l.style.height = 14;
            SetRadius(l, 7);
            l.style.fontSize = 9;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.paddingLeft = 0; l.style.paddingRight = 0; l.style.paddingTop = 0; l.style.paddingBottom = 0;
            l.style.marginLeft = 0; l.style.marginRight = 0; l.style.marginTop = 0; l.style.marginBottom = 0;
            return l;
        }

        static void SetRadius(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = r; e.style.borderTopRightRadius = r;
            e.style.borderBottomLeftRadius = r; e.style.borderBottomRightRadius = r;
        }

        public void Bind(GameObject go, EHSettings s, Color under)
        {
            _go = go;
            string tag = EHCache.Tag(go);
            bool tagDef = EHTagLayer.IsDefaultTag(tag), layDef = EHTagLayer.IsDefaultLayer(go.layer);
            string layerName = EHCache.LayerName(go.layer);
            EHTagLayer.ChipColors(tagDef, EHTagLayer.TagColor(tag), under, out var tagBg, out var tagFg);
            EHTagLayer.ChipColors(layDef, EHTagLayer.LayerColor(go.layer), under, out var layBg, out var layFg);
            tooltip = EHTagLayer.Tooltip(tag, layerName);

            bool combined = s.tagLayerStyle == TagLayerStyle.Combined;
            _combined.style.display = combined ? DisplayStyle.Flex : DisplayStyle.None;
            _circles.style.display = combined ? DisplayStyle.None : DisplayStyle.Flex;

            if (combined)
            {
                float half = s.tagLayerMaxWidth * 0.5f;
                _tag.text = tagDef ? "TAG" : tag;
                _layer.text = layDef ? "LAY" : layerName;
                _tag.style.maxWidth = half;
                _layer.style.maxWidth = half;
                _tag.style.backgroundColor = tagBg; _tag.style.color = tagFg;
                _layer.style.backgroundColor = layBg; _layer.style.color = layFg;
            }
            else
            {
                _t.style.backgroundColor = tagBg; _t.style.color = tagFg;
                _l.style.backgroundColor = layBg; _l.style.color = layFg;
                _t.tooltip = tooltip;
                _l.tooltip = tooltip;
            }
        }

        void Open(PointerDownEvent e, EHTagLayerPopup.Mode mode, VisualElement anchor)
        {
            if (e.button != 0 || _go == null) return;
            e.StopPropagation();
            var wb = anchor.worldBound;
            var screen = wb;
            foreach (var w in Resources.FindObjectsOfTypeAll<HierarchyWindow>())
            {
                if (w.rootVisualElement != null && w.rootVisualElement.panel == panel)
                {
                    screen = new Rect(w.position.x + wb.x, w.position.y + wb.y, wb.width, wb.height);
                    break;
                }
            }
            EHTagLayerPopup.Open(screen, EHTagLayer.Targets(_go), mode);
        }
    }
}
namespace EmpathyHierarchy
{
    /// <summary>Small ▾ next to a scene header's name that opens the scene selector.</summary>
    class EHSceneButton : Label
    {
        EHSceneButton() : base("▾")
        {
            tooltip = "Scenes  (Alt + Shift + S)";
            style.fontSize = 13;
            style.unityTextAlign = TextAnchor.MiddleCenter;
            style.width = 18; style.height = 16;
            style.position = Position.Absolute;
            style.marginLeft = 0; style.marginRight = 0;
            style.paddingLeft = 0; style.paddingRight = 0; style.paddingTop = 0; style.paddingBottom = 1;
            style.color = EHEditorColors.Text;
            style.borderTopLeftRadius = 4; style.borderTopRightRadius = 4;
            style.borderBottomLeftRadius = 4; style.borderBottomRightRadius = 4;
            RegisterCallback<PointerEnterEvent>(_ => style.backgroundColor = EHEditorColors.ButtonHover);
            RegisterCallback<PointerLeaveEvent>(_ => style.backgroundColor = StyleKeyword.Null);
            RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                e.StopImmediatePropagation(); // don't select / fold the scene row
                var win = EditorWindow.mouseOverWindow ?? EditorWindow.focusedWindow;
                var wb = worldBound;
                var origin = win != null ? win.position.position : Vector2.zero;
                EHScenePopup.Open(new Rect(origin.x + wb.x - 4f, origin.y + wb.y, wb.width, wb.height));
            }, TrickleDown.TrickleDown);
        }

        Label _name;
        HierarchyViewItem _item;
        EventCallback<GeometryChangedEvent> _onGeometry;

        /// <summary>Floats right after the scene name's text (the name label itself may span the row).</summary>
        public static void Attach(HierarchyViewItem item)
        {
            var name = item.Name;
            if (name == null) return;
            if (item.Q<EHSceneButton>() != null) return;
            var b = new EHSceneButton { _name = name, _item = item };
            b._onGeometry = _ => b.Place();
            name.RegisterCallback(b._onGeometry);
            item.Add(b);
            b.schedule.Execute(b.Place);
        }

        public static void Detach(HierarchyViewItem item)
        {
            var b = item.Q<EHSceneButton>();
            if (b == null) return;
            b._name?.UnregisterCallback(b._onGeometry);
            b.RemoveFromHierarchy();
        }

        void Place()
        {
            if (_name == null || _item == null || _name.panel == null) return;
            var textW = _name.MeasureTextSize(_name.text, 0, MeasureMode.Undefined, 0, MeasureMode.Undefined).x;
            var nb = _name.worldBound;
            var ib = _item.worldBound;
            if (float.IsNaN(nb.x) || nb.width <= 0f) return;
            float x = nb.x - ib.x + _name.resolvedStyle.paddingLeft + textW + 4f;
            style.left = Mathf.Min(x, ib.width - 40f);
            style.top = nb.y - ib.y + (nb.height - 16f) * 0.5f;
        }
    }
}
#endif
