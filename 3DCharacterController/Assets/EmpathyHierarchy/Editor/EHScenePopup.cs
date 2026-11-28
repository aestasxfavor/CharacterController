using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EmpathyHierarchy
{
    /// <summary>
    /// Scene selector: search, favorites on top (drag to reorder, rows glide into place),
    /// then every scene in Assets. Click opens; + adds a scene to the open ones, − takes it out.
    /// Opened from the ▾ next to a scene header in the Hierarchy, or Alt + Shift + S.
    /// </summary>
    public class EHScenePopup : EditorWindow
    {
        const float W = 290f, RowH = 24f, Pad = 8f, SearchH = 34f, HeaderH = 22f, DividerH = 11f, FooterH = 26f, MaxH = 500f;

        struct SceneInfo
        {
            public string guid, path, name;
        }

        readonly List<SceneInfo> _all = new List<SceneInfo>();
        readonly Dictionary<string, SceneInfo> _byGuid = new Dictionary<string, SceneInfo>();
        readonly List<SceneInfo> _favs = new List<SceneInfo>();
        readonly List<SceneInfo> _rest = new List<SceneInfo>();
        readonly HashSet<string> _loaded = new HashSet<string>();
        readonly Dictionary<string, float> _favY = new Dictionary<string, float>(); // animated row offsets

        string _query = "", _filteredFor;
        Vector2 _scroll;
        bool _focusSearch = true;
        double _lastTime;

        // favorite drag
        int _pressFav = -1;
        bool _draggingFav;
        Vector2 _pressPos;

        GUIStyle _row, _rowBold, _section, _hint, _search;
        static readonly GUIContent Temp = new GUIContent();
        static Texture _sceneIcon;

        [MenuItem("Tools/Empathy Hierarchy/Scene Selector", false, 22)]
        static void OpenFromMenuItem() => OpenFromMenu();

        // a Shortcut (not a menu hotkey), so a clash can be resolved / rebound in Edit > Shortcuts
        [UnityEditor.ShortcutManagement.Shortcut("Empathy Hierarchy/Scene Selector", KeyCode.S, UnityEditor.ShortcutManagement.ShortcutModifiers.Alt | UnityEditor.ShortcutManagement.ShortcutModifiers.Shift)]
        static void OpenFromMenu()
        {
            var anchor = focusedWindow != null ? focusedWindow.position : EditorGUIUtility.GetMainWindowPosition();
            Open(new Rect(anchor.x + 20f, anchor.y + 24f, 1f, 1f));
        }

        public static void Open(Rect screenRect)
        {
            foreach (var existing in Resources.FindObjectsOfTypeAll<EHScenePopup>()) existing.Close();
            var w = CreateInstance<EHScenePopup>();
            w.Collect();
            w.wantsMouseMove = true;
            w.ShowAsDropDown(screenRect, new Vector2(W, w.CalcHeight()));
        }

        void Collect()
        {
            _all.Clear();
            _byGuid.Clear();
            foreach (var guid in AssetDatabase.FindAssets("t:SceneAsset", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var info = new SceneInfo { guid = guid, path = path, name = Path.GetFileNameWithoutExtension(path) };
                _all.Add(info);
                _byGuid[guid] = info;
            }
            _all.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));

            _loaded.Clear();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var sc = SceneManager.GetSceneAt(i);
                if (sc.isLoaded && !string.IsNullOrEmpty(sc.path)) _loaded.Add(sc.path);
            }
            _filteredFor = null;
        }

        void Filter()
        {
            if (_filteredFor == _query) return;
            _filteredFor = _query;
            _favs.Clear();
            _rest.Clear();
            string q = _query.Trim();
            var favSet = new HashSet<string>();
            foreach (var guid in EHSettings.instance.favoriteScenes)
            {
                if (!_byGuid.TryGetValue(guid, out var info)) continue;
                favSet.Add(guid);
                if (Matches(info, q)) _favs.Add(info);
            }
            foreach (var info in _all)
                if (!favSet.Contains(info.guid) && Matches(info, q)) _rest.Add(info);
        }

        static bool Matches(SceneInfo info, string q) =>
            q.Length == 0 || info.name.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) >= 0;

        float ListHeight()
        {
            Filter();
            float h = 4f;
            if (_favs.Count > 0) h += HeaderH + _favs.Count * RowH + DividerH;
            h += HeaderH + Mathf.Max(1, _rest.Count) * RowH;
            return h + 4f;
        }

        float CalcHeight() => Mathf.Min(SearchH + ListHeight() + FooterH, MaxH);

        void Styles()
        {
            if (_row != null) return;
            _row = new GUIStyle(EditorStyles.label) { fontSize = 12, alignment = TextAnchor.MiddleLeft, normal = { textColor = EHTheme.Text } };
            _rowBold = new GUIStyle(_row) { fontStyle = FontStyle.Bold };
            _section = new GUIStyle(EditorStyles.miniBoldLabel) { fontSize = 10, normal = { textColor = EHTheme.Dim } };
            _hint = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = EHTheme.Dim } };
            _search = new GUIStyle(EditorStyles.textField)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(0, 4, 0, 0),
                normal = { background = null, textColor = EHTheme.Text },
                focused = { background = null, textColor = EHTheme.Text },
                hover = { background = null, textColor = EHTheme.Text },
                active = { background = null, textColor = EHTheme.Text },
            };
            if (_sceneIcon == null) _sceneIcon = EditorGUIUtility.IconContent("SceneAsset Icon").image;
        }

        void OnGUI()
        {
            Styles();
            var e = Event.current;
            if (e.type == EventType.MouseMove) Repaint();
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { Close(); e.Use(); return; }

            var full = new Rect(0, 0, position.width, position.height);
            EditorGUI.DrawRect(full, EHTheme.Bg);
            EHDraw.RoundedBorder(new Rect(0.5f, 0.5f, full.width - 1f, full.height - 1f), EHTheme.Divider, 6f, 1f);

            DrawSearch(new Rect(Pad, Pad, full.width - Pad * 2f, SearchH - Pad - 2f));
            Filter();

            // Enter opens the first match
            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
            {
                var first = _favs.Count > 0 ? _favs[0] : _rest.Count > 0 ? _rest[0] : default;
                if (!string.IsNullOrEmpty(first.path)) OpenScene(first, false);
                e.Use();
                return;
            }

            var listRect = new Rect(0, SearchH, full.width, full.height - SearchH - FooterH);
            var content = new Rect(0, 0, full.width - 14f, ListHeight());
            _scroll = GUI.BeginScrollView(listRect, _scroll, content);
            float y = 4f, w = content.width;

            if (_favs.Count > 0)
            {
                GUI.Label(new Rect(Pad + 4f, y + 4f, w, 16f), "FAVORITES", _section);
                y += HeaderH;
                DrawFavorites(y, w);
                y += _favs.Count * RowH;
                EHDraw.Rect(new Rect(Pad + 4f, y + 5f, w - Pad - 8f, 1f), EHTheme.Divider);
                y += DividerH;
            }

            GUI.Label(new Rect(Pad + 4f, y + 4f, w, 16f), "ALL SCENES  (" + _rest.Count + ")", _section);
            y += HeaderH;
            if (_rest.Count == 0)
                GUI.Label(new Rect(Pad, y, w - Pad, RowH), _query.Length > 0 ? "No match" : "No scenes in Assets", _hint);
            for (int i = 0; i < _rest.Count; i++)
            {
                var r = new Rect(Pad, y + i * RowH, w - Pad, RowH - 2f);
                if (r.yMax < _scroll.y || r.y > _scroll.y + listRect.height) continue; // off screen
                SceneRow(r, _rest[i], false, -1);
            }
            GUI.EndScrollView();

            var footer = new Rect(Pad, full.height - FooterH, full.width - Pad * 2f, FooterH);
            EHDraw.Rect(new Rect(Pad, footer.y, footer.width, 1f), EHTheme.Divider);
            GUI.Label(footer, "Click: open   ·   + add   ·   − remove   ·   Drag ★ to reorder", _hint);
        }

        void DrawSearch(Rect r)
        {
            if (Event.current.type == EventType.Repaint)
            {
                EHDraw.Rounded(r, EHTheme.Card, 7f);
                EHDraw.Text(new Rect(r.x + 8f, r.y, 16f, r.height), "⌕", EHDraw.Label(false), EHTheme.Dim, TextAnchor.MiddleCenter);
            }
            GUI.SetNextControlName("EHSceneSearch");
            var field = new Rect(r.x + 26f, r.y + 3f, r.width - 32f, r.height - 6f);
            string q = GUI.TextField(field, _query, _search);
            if (q != _query)
            {
                _query = q;
                _scroll = Vector2.zero;
            }
            if (_query.Length == 0 && Event.current.type == EventType.Repaint && GUI.GetNameOfFocusedControl() != "EHSceneSearch")
                EHDraw.Text(field, "Search scenes", EHDraw.Label(false), EHTheme.Dim, TextAnchor.MiddleLeft);
            if (_focusSearch && Event.current.type == EventType.Repaint)
            {
                _focusSearch = false;
                EditorGUI.FocusTextInControl("EHSceneSearch");
            }
        }

        // ------------------------------------------------------------------ favorites (drag to reorder)

        void DrawFavorites(float top, float w)
        {
            var e = Event.current;
            bool canDrag = _query.Trim().Length == 0;

            // glide rows toward their slots
            double now = EditorApplication.timeSinceStartup;
            float dt = Mathf.Clamp((float)(now - _lastTime), 0f, 0.1f);
            if (e.type == EventType.Repaint) _lastTime = now;
            bool moving = false;

            for (int i = 0; i < _favs.Count; i++)
            {
                var info = _favs[i];
                float target = i * RowH;
                if (!_favY.TryGetValue(info.guid, out var cur)) cur = target;
                if (e.type == EventType.Repaint)
                {
                    cur = Mathf.Lerp(cur, target, 1f - Mathf.Exp(-dt * 18f));
                    if (Mathf.Abs(cur - target) < 0.3f) cur = target;
                    else moving = true;
                    _favY[info.guid] = cur;
                }
                var r = new Rect(Pad, top + cur, w - Pad, RowH - 2f);
                bool dragged = _draggingFav && i == _pressFav;
                if (dragged && e.type == EventType.Repaint)
                    EHDraw.RoundedBorder(r, EHTheme.Point, 6f, 1.5f);
                SceneRow(r, info, true, canDrag ? i : -1);
            }

            if (_pressFav >= 0)
            {
                if (e.type == EventType.MouseDrag)
                {
                    if (!_draggingFav && (e.mousePosition - _pressPos).sqrMagnitude > 16f) _draggingFav = true;
                    if (_draggingFav)
                    {
                        int target = Mathf.Clamp(Mathf.FloorToInt((e.mousePosition.y - top) / RowH), 0, _favs.Count - 1);
                        if (target != _pressFav)
                        {
                            var item = _favs[_pressFav];
                            _favs.RemoveAt(_pressFav);
                            _favs.Insert(target, item);
                            _pressFav = target;
                        }
                        Repaint();
                    }
                    e.Use();
                }
                else if (e.rawType == EventType.MouseUp)
                {
                    if (_draggingFav) SaveFavoriteOrder();
                    _pressFav = -1;
                    _draggingFav = false;
                    Repaint();
                }
            }
            if (moving) Repaint();
        }

        void SaveFavoriteOrder()
        {
            var list = EHSettings.instance.favoriteScenes;
            list.Clear();
            foreach (var f in _favs) list.Add(f.guid);
            EHSettings.instance.SaveSettings();
            _filteredFor = null;
        }

        // ------------------------------------------------------------------ rows

        void SceneRow(Rect r, SceneInfo info, bool fav, int favIndex)
        {
            var e = Event.current;
            bool hover = r.Contains(e.mousePosition) && !_draggingFav;
            bool loaded = _loaded.Contains(info.path);
            var star = new Rect(r.xMax - 26f, r.y, 24f, r.height);
            bool starHover = star.Contains(e.mousePosition);
            // + adds the scene next to the open ones; − (shown on open scenes) takes it out
            var addRect = new Rect(star.x - 24f, r.y + 2f, 22f, r.height - 4f);
            bool addHover = addRect.Contains(e.mousePosition);
            bool canRemove = loaded && LoadedCount() > 1;

            if (e.type == EventType.Repaint)
            {
                if (loaded) EHDraw.Rounded(r, EHDraw.WithAlpha(EHTheme.Accent, 0.12f), 6f);
                if (hover) EHDraw.Rounded(r, EHTheme.CardHover, 6f);
                if (_sceneIcon != null)
                    GUI.DrawTexture(new Rect(r.x + 8f, r.center.y - 8f, 16f, 16f), _sceneIcon, ScaleMode.ScaleToFit,
                        true, 0f, loaded ? Color.white : new Color(1f, 1f, 1f, 0.75f), 0f, 0f);
                Temp.text = info.name;
                Temp.tooltip = null;
                (loaded ? _rowBold : _row).Draw(new Rect(r.x + 30f, r.y, r.width - 84f, r.height), Temp, false, false, false, false);
                if (loaded || hover)
                {
                    string sign = loaded ? "−" : "+";
                    Color c = loaded ? (canRemove ? (addHover ? Color.white : new Color(0.95f, 0.62f, 0.6f)) : EHTheme.Dim)
                                     : (addHover ? EHTheme.AccentText : EHTheme.Point);
                    if (addHover && (!loaded || canRemove))
                        EHDraw.Rounded(addRect, loaded ? new Color(0.78f, 0.22f, 0.22f) : EHTheme.Point, 5f);
                    EHDraw.Text(addRect, sign, EHDraw.Label(true), c, TextAnchor.MiddleCenter);
                }
                if (fav || hover)
                    EHDraw.Text(star, fav ? "★" : "☆", EHDraw.Label(false),
                        fav ? (starHover ? EHTheme.Text : EHTheme.Point) : (starHover ? EHTheme.Point : EHTheme.Dim),
                        TextAnchor.MiddleCenter);
            }

            if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
            {
                if (starHover)
                {
                    ToggleFavorite(info.guid);
                    e.Use();
                    return;
                }
                if (addHover)
                {
                    if (loaded) { if (canRemove) RemoveScene(info); }
                    else OpenScene(info, true);
                    e.Use();
                    return;
                }
                if (favIndex >= 0)
                {
                    // could be a click or the start of a drag: decide on mouse up
                    _pressFav = favIndex;
                    _pressPos = e.mousePosition;
                    _draggingFav = false;
                    e.Use();
                    return;
                }
                OpenScene(info, false);
                e.Use();
            }

            if (favIndex >= 0 && e.rawType == EventType.MouseUp && _pressFav == favIndex && !_draggingFav &&
                r.Contains(e.mousePosition))
            {
                _pressFav = -1;
                OpenScene(info, false);
                e.Use();
            }
        }

        int LoadedCount()
        {
            int n = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isLoaded) n++;
            return n;
        }

        void RefreshLoaded()
        {
            if (this == null) return;
            _loaded.Clear();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var sc = SceneManager.GetSceneAt(i);
                if (sc.isLoaded && !string.IsNullOrEmpty(sc.path)) _loaded.Add(sc.path);
            }
            Repaint();
        }

        /// <summary>Takes an open scene out (asks to save it first if it has changes). The last open scene stays.</summary>
        void RemoveScene(SceneInfo info)
        {
            if (EditorApplication.isPlaying)
            {
                ShowNotification(new GUIContent("Exit Play Mode first"), 1.2);
                return;
            }
            string path = info.path;
            EditorApplication.delayCall += () =>
            {
                var sc = SceneManager.GetSceneByPath(path);
                if (!sc.IsValid() || !sc.isLoaded || LoadedCount() <= 1) return;
                if (!EditorSceneManager.SaveModifiedScenesIfUserWantsTo(new[] { sc })) return;
                EditorSceneManager.CloseScene(sc, true);
                RefreshLoaded();
            };
        }

        void ToggleFavorite(string guid)
        {
            var list = EHSettings.instance.favoriteScenes;
            if (!list.Remove(guid)) list.Add(guid);
            EHSettings.instance.SaveSettings();
            _filteredFor = null;
            Filter();
            // resize to fit the new layout
            var p = position;
            float h = CalcHeight();
            if (!Mathf.Approximately(p.height, h))
            {
                minSize = maxSize = new Vector2(W, h);
                position = new Rect(p.x, p.y, W, h);
            }
            Repaint();
        }

        void OpenScene(SceneInfo info, bool additive)
        {
            if (EditorApplication.isPlaying)
            {
                ShowNotification(new GUIContent("Exit Play Mode first"), 1.2);
                return;
            }
            if (additive)
            {
                if (_loaded.Contains(info.path)) return;
                string add = info.path;
                EditorApplication.delayCall += () =>
                {
                    EditorSceneManager.OpenScene(add, OpenSceneMode.Additive);
                    RefreshLoaded();
                };
                return; // stay open: add / remove several in a row
            }
            // open after this GUI event has finished: loading a scene in the middle of an
            // OnGUI pass makes the renderer (GPU Resident Drawer) draw with stale batches
            string path = info.path;
            Close();
            EditorApplication.delayCall += () =>
            {
                if (!additive && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(path, additive ? OpenSceneMode.Additive : OpenSceneMode.Single);
            };
            GUIUtility.ExitGUI();
        }
    }
}
