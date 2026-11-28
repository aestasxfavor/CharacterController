using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace EmpathyHierarchy
{
    /// <summary>
    /// Bookmarks as their own window (Tab mode; always used with the classic Hierarchy).
    /// Wide and short → chips in rows with the buttons on the right; narrow or tall → one chip per row, buttons on top.
    /// </summary>
    class EHBookmarkWindow : EditorWindow
    {
        static string OpenedKey => "EmpathyHierarchy.BookmarkWindowOpened." + Application.dataPath.GetHashCode();

        EHNavBar _bar;

        /// <summary>True until the window has been opened once automatically in this project (per user).</summary>
        internal static bool FirstTimeForProject => !EditorPrefs.GetBool(OpenedKey, false);

        internal static bool Wanted
        {
            get
            {
                var s = EHSettings.instance;
                return s.enabled && s.navBar && (!EHSettings.NewHierarchyActive || s.navBarMode == NavBarMode.Tab);
            }
        }

        /// <summary>Closes the window when bookmarks shouldn't be in a tab; opens it if asked and missing.</summary>
        internal static void Sync(bool openIfMissing)
        {
            var open = Resources.FindObjectsOfTypeAll<EHBookmarkWindow>();
            if (!Wanted)
            {
                foreach (var w in open) w.Close();
                return;
            }
            if (open.Length == 0 && openIfMissing) OpenDocked();
        }

        [MenuItem("Tools/Empathy Hierarchy/Bookmarks Window", false, 20)]
        static void Menu()
        {
            var open = Resources.FindObjectsOfTypeAll<EHBookmarkWindow>();
            if (open.Length > 0) open[0].Focus();
            else OpenDocked();
        }

        /// <summary>Opens the window next to the Hierarchy as its own window (dock it anywhere by its tab).</summary>
        internal static void OpenDocked()
        {
            EditorPrefs.SetBool(OpenedKey, true);
            var w = CreateInstance<EHBookmarkWindow>();
            w.titleContent = new GUIContent("★ Bookmarks");
            w.minSize = new Vector2(120, 34);
            var hierarchy = EHNav.FindHierarchy();
            if (hierarchy != null)
            {
                var h = hierarchy.position;
                w.position = new Rect(h.xMax + 4, h.y, 200, Mathf.Max(200, h.height));
            }
            w.Show();
        }

        void CreateGUI()
        {
            titleContent = new GUIContent("★ Bookmarks");
            var root = rootVisualElement;
            root.style.backgroundColor = EHEditorColors.BarBg;

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            root.Add(scroll);

            _bar = new EHNavBar(this, EHNavFilters.Current, true);
            scroll.Add(_bar);
            _bar.RegisterDropTarget(root); // drop anywhere in the window

            root.RegisterCallback<GeometryChangedEvent>(_ => UpdateLayout());
        }

        void UpdateLayout()
        {
            if (_bar == null) return;
            var r = rootVisualElement.contentRect;
            if (r.width <= 0f) return;
            // docked at the side (narrow or taller than wide) → list; docked above / below → rows
            _bar.SetVertical(r.width < 240f || r.height > r.width);
        }
    }
}
