using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Rowlan.SceneNav
{
    public class SceneNavDataEditorWindow : EditorWindow
    {
        [MenuItem(ProjectSetup.MENU, false, ProjectSetup.MENU_ORDER)]
        static void CreateWindow()
        {
            SceneNavDataEditorWindow wnd = EditorWindow.GetWindow<SceneNavDataEditorWindow>();
            wnd.titleContent.text = ProjectSetup.WINDOW_TITLE;

            wnd.position = new Rect(50, 50, 400, 720);
            //wnd.Close();
        }

        private SceneNavDataEditorWindow editorWindow;

        private Editor sceneNavDataeditor;
        private SceneNavData sceneNavData;

        private void OnEnable()
        {
            editorWindow = this;

            ScriptableObjectManager<SceneNavData> settingsManager = new ScriptableObjectManager<SceneNavData>(ProjectSetup.SETTINGS_FOLDER, ProjectSetup.SETTINGS_FILENAME);
            sceneNavData = settingsManager.GetAsset();

            sceneNavDataeditor = Editor.CreateEditor(sceneNavData);
        }

        private void OnGUI()
        {
            sceneNavDataeditor.OnInspectorGUI();
        }
    }
}
