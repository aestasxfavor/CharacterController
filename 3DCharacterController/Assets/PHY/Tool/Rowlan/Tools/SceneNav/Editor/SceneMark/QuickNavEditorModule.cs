using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Rowlan.SceneMark
{
    public class QuickNavEditorModule
    {
        public enum ModuleType
        {
            History,
            Favorites,
        }

        private QuickNavListControl quickNavListControl;
        private Vector2 quickNavListScrollPosition;

        private QuickNavDataManager dataManager;

        private SerializedObject serializedObject;
        private SerializedProperty serializedProperty;
        private QuickNavList quickNavList;


        public string headerText = "";
        public bool reorderEnabled = false;
        public bool addSelectedEnabled = false;
        public ModuleType moduleType;

        // icons depending on the navigation direction
        private GUIContent previousIcon;
        private GUIContent nextIcon;

        public QuickNavEditorModule( QuickNavDataManager dataManager, ModuleType moduleType)
        {
            this.dataManager = dataManager;

            this.moduleType = moduleType;
            this.serializedObject = dataManager.GetSerializedObject();
            this.serializedProperty = dataManager.GetListProperty(moduleType);
            this.quickNavList = dataManager.GetQuickNavList(moduleType);

            // setup styles, icons etc
            SetupStyles();
        }

        public ModuleType GetModuleType()
        {
            return moduleType;
        }

        /// <summary>
        /// Setup styles, icons, etc
        /// </summary>
        private void SetupStyles()
        {
            switch (moduleType)
            {
                case ModuleType.History: // fallthrough
                    previousIcon = GUIStyles.LeftIcon;
                    nextIcon = GUIStyles.RightIcon;
                    break;

                case ModuleType.Favorites:
                    previousIcon = GUIStyles.DownIcon;
                    nextIcon = GUIStyles.UpIcon;
                    break;

                default: throw new System.Exception($"Unsupported module type: {moduleType}");
            }
        }

        public QuickNavList GetQuickNavItemList()
        {
            return quickNavList;
        }

        public void OnEnable()
        {
            // initialize UI components
            quickNavListControl = new QuickNavListControl(this, headerText, reorderEnabled, serializedObject, serializedProperty);
        }

        public void OnGUI()
        {
            // EditorGUILayout.LabelField(string.Format("Current QuickNav Index: {0}", currentSelectionHistoryIndex));

            GUILayout.Space(6);

            GUILayout.BeginHorizontal();
            {
                if (GUILayout.Button(previousIcon, GUILayout.Width(80), GUILayout.Height( GUIStyles.TOOLBAR_BUTTON_HEIGHT)))
                {
                    int selectionIndex = quickNavListControl.Next();

                    JumpToSceneMark( selectionIndex);
                }

                if (GUILayout.Button(nextIcon, GUILayout.Width(80), GUILayout.Height(GUIStyles.TOOLBAR_BUTTON_HEIGHT)))
                {
                    int selectionIndex = quickNavListControl.Previous();

                    JumpToSceneMark( selectionIndex);


                }

                GUILayout.FlexibleSpace();

                if(addSelectedEnabled)
                {

                    if (GUILayout.Button(GUIStyles.AddIcon, GUILayout.Width(60), GUILayout.Height(GUIStyles.TOOLBAR_BUTTON_HEIGHT)))
                    {
                        dataManager.AddCurrentSceneViewToFavorites();
                    }

                }

                if (GUILayout.Button(GUIStyles.ClearIcon, GUILayout.Height(GUIStyles.TOOLBAR_BUTTON_HEIGHT)))
                {
                    dataManager.Clear( moduleType);

                    quickNavListControl.Reset();

                    EditorUtility.SetDirty(serializedObject.targetObject);
                }

            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // show history list
            quickNavListScrollPosition = EditorGUILayout.BeginScrollView(quickNavListScrollPosition, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            {
                quickNavListControl.DoLayoutList();

                // context popup menu
                if (moduleType == ModuleType.Favorites)
                {
                    switch(Event.current.type)
                    {
                        case EventType.ContextClick:
                            Vector2 position = Event.current.mousePosition;
                            Rect popupRect = new Rect(position, Vector2.zero); // 2nd parameter is the offset from mouse position

                            PopupWindow.Show(popupRect, new QuickNavPopupWindow(this, headerText));

                            Event.current.Use();
                            break;

                        case EventType.DragUpdated:
                            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                            break;

                        case EventType.DragPerform:
                            DragAndDrop.AcceptDrag();

                            dataManager.AddToFavorites(DragAndDrop.objectReferences);

                            Event.current.Use();
                            break;
                    }
                }

            }
            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Get the number of items in the list
        /// </summary>
        /// <returns></returns>
        public int GetItemCount()
        {
            // TODO: use only the serializedProperty, don't hand over the quicknavlist
            // for some reason we can't access objectReferenceValue from the serializedProperty (might be Unity bug)
            // another thing is that in QuickNavEditorWindow OnSelectionChange is invoked while the object is loadeded
            // eg on scriptchange the array index becomes invalid when an object is selected in the hierarchy and the
            // editorwindow is open; a race condition which can be solved, but not necessarily => handing over the 
            // list is currently working fine and can be solved later, objectReferenceValue would be more important
            return GetQuickNavItemList().Count();
        }

        public QuickNavItem GetCurrentQuickNavItem()
        {
            if (GetItemCount() == 0)
                return null;

            int selectionIndex = quickNavListControl.GetCurrentSelectionIndex();

            if (selectionIndex < 0 || selectionIndex >= GetItemCount())
                return null;

            QuickNavItem quickNavItem = GetQuickNavItemList().GetItemAt( selectionIndex);

            return quickNavItem;
        }

        /// <summary>
        /// Get the current quick nav item and jump to it by selecting it
        /// </summary>
        public void JumpToQuickNavItem( bool openInInspector, int selectionIndex)
        {
            QuickNavItem quickNavItem = GetCurrentQuickNavItem();

            if (quickNavItem == null)
                return;

            // select in reorderable list
            quickNavListControl.index = selectionIndex;
            //reorderableList.Select(currentSelectionIndex);

            // select the object and open it in the inspector
            if (openInInspector)
            {
                // selection objects
                UnityEngine.Object[] objects = new UnityEngine.Object[] { quickNavItem.unityObject };

                // select objects
                Selection.objects = objects;
            }
            // just select the object, don't open it in the inspector
            else
            {
                
                EditorGUIUtility.PingObject(quickNavItem.unityObject);

                // alternative: open in application (eg doubleclick)
                // AssetDatabase.OpenAsset(quickNavItem.unityObject);

            }

        }

        public void JumpToSceneMark(int selectionIndex)
        {
            QuickNavItem quickNavItem = GetCurrentQuickNavItem();

            if (quickNavItem == null)
                return;

            // select in reorderable list
            quickNavListControl.index = selectionIndex;
            //quickNavListControl.Select(selectionIndex);

            Vector3 position = quickNavItem.sceneMark.sceneViewPosition;
            Quaternion rotation = quickNavItem.sceneMark.sceneViewRotation;
            float size = quickNavItem.sceneMark.sceneViewSize;

            // direct jump
            //SceneView.lastActiveSceneView.pivot = position; 
            //SceneView.lastActiveSceneView.rotation = rotation;

            // smooth animation https://docs.unity3d.com/ScriptReference/SceneView.LookAt.html
            SceneView.lastActiveSceneView.LookAt(position, rotation, size, false, false); 

        }

        public QuickNavDataManager GetDataManager()
        {
            return dataManager;
        }

        /// <summary>
        /// Check if the currently selected item matches the first in the list.
        /// This is used with the history module in order to not add tuplicates to the FIFO queue.
        /// </summary>
        /// <returns></returns>
        public bool CurrentSelectionMatchesFirstItem()
        {
#if UNITY_6000_5_OR_NEWER
int length = Selection.entityIds.Length;
#else
int length = Selection.instanceIDs.Length;
#endif
            if (length == 1)
            {
                QuickNavItem quickNavItem = GetCurrentQuickNavItem();
                if (quickNavItem != null)
                {
                    if (quickNavItem.unityObject == Selection.objects[0])
                        return true;
                }
            }

            return false;
        }
    }
}
