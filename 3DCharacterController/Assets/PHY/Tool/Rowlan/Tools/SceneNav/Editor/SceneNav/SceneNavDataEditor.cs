using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEditorInternal;
using UnityEngine;

namespace Rowlan.SceneNav
{
    [CustomEditor(typeof(SceneNavData))]
    public class SceneNavDataEditor : Editor
    {
        private SceneNavDataEditor editor;
        private SceneNavData editorTarget;

        private PresetListControl presetListControl;
        private Vector2 presetListScrollPosition;

        private bool reorderEnabled = true;

        void OnEnable()
        {
            editor = this;
            editorTarget = (SceneNavData) target;

            // initialize UI components
            SerializedProperty serializedProperty = serializedObject.FindProperty("presets");

            presetListControl = new PresetListControl( editor, reorderEnabled, serializedObject, serializedProperty);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            /// 
            /// info & help
            /// 
            GUILayout.BeginVertical(GUIStyles.HelpBoxStyle);
            {
                EditorGUILayout.BeginHorizontal();
                {

                    if (GUILayout.Button("Asset Store", EditorStyles.miniButton, GUILayout.Width(120)))
                    {
                        Application.OpenURL("https://u3d.as/2PAs");
                    }

                    if (GUILayout.Button("Documentation", EditorStyles.miniButton))
                    {
                        Application.OpenURL("https://bit.ly/scenenav-doc");
                    }

                    if (GUILayout.Button("Forum", EditorStyles.miniButton, GUILayout.Width(120)))
                    {
                        Application.OpenURL("https://forum.unity.com/");
                    }

                    if (GUILayout.Button(new GUIContent("?", "Help box visibility"), EditorStyles.miniButton, GUILayout.Width(20)))
                    {
                        // toggle help box visibility
                        editorTarget.helpBoxVisible = !editorTarget.helpBoxVisible;
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();

            ///
            /// header
            /// 
            GUILayout.BeginVertical(GUIStyles.AppTitleBoxStyle);
            {
                EditorGUILayout.LabelField("SceneNav", GUIStyles.AppTitleBoxStyle, GUILayout.Height(30)); // app title box style for double border for emphasis; as beginvertical and label
            }
            GUILayout.EndVertical();

            ///
            /// usage
            /// 
            if (editorTarget.helpBoxVisible)
            {
                GUILayout.BeginVertical(GUIStyles.HelpBoxStyle);
                {
                    EditorGUILayout.HelpBox("SceneNav allows you to quickly navigate in the scene by zooming out, showing a preview render of the target at the mouse location and zooming back in.", MessageType.None);
                    EditorGUILayout.HelpBox("Usage: Press and hold a shortcut key, move the mouse in the sceneview to show the target, release the shortcut key to move to the target location. Press right mouse button to abort."
                    + "\n\nThe shortcut keys provide presets with different settings which can adjusted to your personal preference.", MessageType.None);

                }
                GUILayout.EndVertical();
            }

            ///
            /// toolbar
            /// 
            GUILayout.BeginVertical(GUIStyles.GroupBoxStyle);
            {
                GUILayout.BeginHorizontal();
                {
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Reset", GUILayout.Width(100)))
                    {
                        Undo.RegisterCompleteObjectUndo(target, "SceneNav Reset");

                        editorTarget.Reset();

                        EditorUtility.SetDirty(editor.target);
                    }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();

            ///
            /// general settings
            /// 
            GUILayout.BeginVertical(GUIStyles.GroupBoxStyle);
            {
                EditorGUILayout.LabelField("General", GUIStyles.BoxTitleStyle);

                editorTarget.assetEnabled = EditorGUILayout.Toggle("SceneNav Enabled", editorTarget.assetEnabled);
            }
            GUILayout.EndVertical();

            ///
            /// presets
            ///
            GUILayout.Space(6);

            // show history list
            presetListScrollPosition = EditorGUILayout.BeginScrollView(presetListScrollPosition, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            {
                presetListControl.DoLayoutList();
            }
            EditorGUILayout.EndScrollView();

            serializedObject.ApplyModifiedProperties();
        }
    }
}