using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEditorInternal;
using UnityEngine;

namespace Rowlan.SceneNav
{
    public class PresetListControl : ReorderableList
    {
        SceneNavDataEditor editor;

        public PresetListControl(SceneNavDataEditor editor, bool reorderEnabled, SerializedObject serializedObject, SerializedProperty elements) : base(serializedObject, elements)
        {
            this.editor = editor;

            string headerText = "Settings";

            string[] propertyNames = new string[]
            {
                "labelVisible",
                "previewPosition",
                "zoomOutDistance",
                "cameraOffset",
                "renderTextureSize"
            };

            int lineCountPerProperty = propertyNames.Length + 2; // + 1 for the name, +1 for the shortcut

            draggable = reorderEnabled;
            displayAdd = false;
            displayRemove = false;

            // list header
            drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(rect, headerText, GUIStyles.BoxTitleStyle);
            };

            drawElementCallback = (rect, index, active, focused) =>
            {
                // Get the currently to be drawn element from YourList
                SerializedProperty element = serializedProperty.GetArrayElementAtIndex(index);

                // name property for the header
                SerializedProperty nameProperty = element.FindPropertyRelative("name");

                float margin = 3;

                float left = 0;
                float right = 0;
                float width = EditorGUIUtility.currentViewWidth - (right + margin) - margin * 3 - 22;

                Rect lineRect;

                // list item header
                {
                    EditorGUI.LabelField(new Rect(rect.x + left, rect.y + margin, width, EditorGUIUtility.singleLineHeight), new GUIContent(nameProperty.stringValue));
                }

                EditorGUI.indentLevel++;
                {
                    //
                    // shortcut
                    //

                    // get the shortcut text
                    string shortcutText = "<invalid>";
                    string shortcutId = PresetDefaults.PresetShortcutIds[index];
                    ShortcutBinding shortcutBinding = ShortcutManager.instance.GetShortcutBinding(shortcutId);
                    IEnumerator<KeyCombination> enumerator = shortcutBinding.keyCombinationSequence.GetEnumerator();
                    if (enumerator.MoveNext())
                    {
                        KeyCombination keyCombination = enumerator.Current;
                        shortcutText = keyCombination.ToString();
                    }

                    // show shortcut as label in the inspector
                    rect.y += EditorGUIUtility.singleLineHeight;
                    lineRect = new Rect(rect.x + left, rect.y + margin, width, EditorGUIUtility.singleLineHeight);
                    EditorGUI.LabelField(lineRect, "Shortcut", shortcutText);

                    //
                    // data
                    //
                    foreach (string propertyName in propertyNames)
                    {
                        SerializedProperty property = element.FindPropertyRelative(propertyName);

                        rect.y += EditorGUIUtility.singleLineHeight;
                        lineRect = new Rect(rect.x + left, rect.y + margin, width, EditorGUIUtility.singleLineHeight);
                        EditorGUI.PropertyField(lineRect, property);
                    }
                }
                EditorGUI.indentLevel--;

                // advance to next line for the next property
                rect.y += EditorGUIUtility.singleLineHeight;
            };

            elementHeightCallback = (index) =>
            {
                return EditorGUIUtility.singleLineHeight * lineCountPerProperty;
            };
        }

    }
}