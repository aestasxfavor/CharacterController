using Rowlan.SceneNav;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rowlan.SceneMark
{
    public class QuickNavEditorWindow : EditorWindow
    {
        [MenuItem(ProjectSetup.MENU, false, ProjectSetup.MENU_ORDER)]
        static void CreateWindow()
        {
            QuickNavEditorWindow wnd = EditorWindow.GetWindow<QuickNavEditorWindow>();
            wnd.titleContent.text = ProjectSetup.WINDOW_TITLE;

            wnd.position = new Rect(0, 0, 600, 1000);
            //wnd.Close();
        }

        private enum QuickNavTab
        {
            History,
            Favorites
        }

        private GUIContent[] quickNavTabs;
        private int selectedQuickNavTabIndex = 0;

        private QuickNavEditorWindow editorWindow;

        private QuickNavDataManager dataManager = new QuickNavDataManager();

        private QuickNavEditorModule historyModule;
        private QuickNavEditorModule favoritesModule;

        void OnEnable()
        {
            editorWindow = this;

            // initialize data
            dataManager.OnEnable();

            // unity startup, first access
            if ( !Startup.Instance.Initialized)
            {
                // clear history at startup
                dataManager.InitializeHistory();
            }

            // update history and favorites using the object guid
            // this may become necessary after a restart of the editor
            dataManager.Refresh();

            #region Modules

            // history
            historyModule = new QuickNavEditorModule( dataManager, QuickNavEditorModule.ModuleType.History)
            {
                headerText = "History",
                reorderEnabled = false,
                addSelectedEnabled = false,
            };
            historyModule.OnEnable();

            // favorites
            favoritesModule = new QuickNavEditorModule( dataManager, QuickNavEditorModule.ModuleType.Favorites)
            {
                headerText = "Favorites",
                reorderEnabled = true,
                addSelectedEnabled = true,
            };
            favoritesModule.OnEnable();

            #endregion Modules

            quickNavTabs = new GUIContent[]
            {
                new GUIContent( QuickNavTab.History.ToString()),
                new GUIContent( QuickNavTab.Favorites.ToString()),
            };

            Startup.Instance.Initialized = true;

            // hook into the scene change for refresh of the objects when another scene gets loaded
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened -= SceneOpenedCallback;
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += SceneOpenedCallback;

            EnableSceneNavEvents();

        }

        void OnDisable()
        {
            DisableSceneNavEvents();

            // remove scene change hook
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened -= SceneOpenedCallback;
        }

        /// <summary>
        /// Scene change handler
        /// </summary>
        /// <param name="_scene"></param>
        /// <param name="_mode"></param>
        void SceneOpenedCallback(Scene _scene, UnityEditor.SceneManagement.OpenSceneMode _mode)
        {
            // update history and favorites using the object guid
            // this may become necessary after a restart of the editor
            dataManager.Refresh();
        }


        void OnGUI()
        {
            // consider bootstrapping when tabs are initially null
            if (quickNavTabs == null)
                return;

            dataManager.GetSerializedObject().Update();

            selectedQuickNavTabIndex = GUILayout.Toolbar(selectedQuickNavTabIndex, quickNavTabs, GUILayout.Height(20));

            switch(selectedQuickNavTabIndex)
            {
                case ((int)QuickNavTab.History):
                    historyModule.OnGUI();
                    break;

                case ((int)QuickNavTab.Favorites):
                    favoritesModule.OnGUI();
                    break;

            }

            dataManager.GetSerializedObject().ApplyModifiedProperties();
        }

        void OnInspectorUpdate()
        {
            // Call Repaint on OnInspectorUpdate as it repaints the windows
            // less times as if it was OnGUI/Update
            Repaint();
        }

        /// <summary>
        /// Register for SceneNav event handling
        /// </summary>
        void EnableSceneNavEvents()
        {
            // listen to SceneNav events
            SceneNavEventManager.OnZoom -= OnZoom;
            SceneNavEventManager.OnZoom += OnZoom;
        }

        /// <summary>
        /// Unregister SceneNav event handling
        /// </summary>
        void DisableSceneNavEvents()
        {
            SceneNavEventManager.OnZoom -= OnZoom;
        }

        public void OnZoom(ZoomEvent zoomEvent)
        {
            dataManager.AddToHistory(zoomEvent);
        }


    }
}