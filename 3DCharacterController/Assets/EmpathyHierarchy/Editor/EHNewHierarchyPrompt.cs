#if UNITY_6000_5_OR_NEWER
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace EmpathyHierarchy
{
    /// <summary>
    /// Shown once per project (per user) when the classic Hierarchy is in use on Unity 6.5+,
    /// pointing to Preferences › General › Use new Hierarchy window.
    /// </summary>
    class EHNewHierarchyPrompt : EditorWindow
    {
        static string PrefKey => "EmpathyHierarchy.NewHierarchyPrompt." + Application.dataPath.GetHashCode();
        static bool _queued;

        internal static void MaybeShow()
        {
            if (_queued || EditorPrefs.GetBool(PrefKey, false)) return;
            _queued = true;
            EditorApplication.delayCall += () =>
            {
                if (EditorPrefs.GetBool(PrefKey, false)) return;
                EditorPrefs.SetBool(PrefKey, true);
                Open();
            };
        }

        [MenuItem("Tools/Empathy Hierarchy/Use New Hierarchy...", false, 40)]
        static void Open()
        {
            var w = CreateInstance<EHNewHierarchyPrompt>();
            w.titleContent = new GUIContent("Empathy Hierarchy");
            var size = new Vector2(400, 196);
            w.minSize = w.maxSize = size;
            var main = EditorGUIUtility.GetMainWindowPosition();
            w.position = new Rect(main.center - size * 0.5f, size);
            w.ShowUtility();
        }

        void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.backgroundColor = EHTheme.Bg;
            root.style.paddingLeft = 14; root.style.paddingRight = 14;
            root.style.paddingTop = 14; root.style.paddingBottom = 14;

            var box = new VisualElement();
            box.style.flexGrow = 1;
            box.style.backgroundColor = EHTheme.GroupBg;
            box.style.paddingLeft = 14; box.style.paddingRight = 14;
            box.style.paddingTop = 12; box.style.paddingBottom = 12;
            MemoUI.Radius(box, 8);
            MemoUI.Border(box, EHTheme.Divider, 1f);
            root.Add(box);

            var title = new Label("USE THE NEW HIERARCHY");
            title.style.color = EHTheme.Point;
            title.style.fontSize = 12;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 8;
            box.Add(title);

            var body = new Label("Empathy Hierarchy works best with the new Hierarchy window: " +
                                 "the bookmark bar, wide root rows, accent bars and header buttons only appear there.\n" +
                                 "To switch, tick \"Use new Hierarchy window\" under General › New Hierarchy.");
            body.style.color = EHTheme.Text;
            body.style.fontSize = 11;
            body.style.whiteSpace = WhiteSpace.Normal;
            body.style.flexGrow = 1;
            box.Add(body);


            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.FlexEnd;
            box.Add(row);

            var keep = MemoUI.SmallButton("Keep classic", "You can switch later in Preferences › General", Close);
            keep.style.marginRight = 6;
            row.Add(keep);

            var use = MemoUI.SmallButton("Open Preferences", "Opens Preferences › General › New Hierarchy", UseNew);
            use.userData = true; // fixed colours, no hover swap
            use.style.backgroundColor = EHTheme.Accent;
            use.style.color = EHTheme.AccentText;
            use.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(use);
        }

        void UseNew()
        {
            // Unity keeps this switch internal, so open the page where it lives
            SettingsService.OpenUserPreferences("Preferences/General");
            Close();
        }
    }
}
#endif
