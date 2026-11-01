using Rowlan.SceneNav;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static Rowlan.SceneMark.QuickNavEditorModule;
using static Rowlan.SceneNav.ZoomEvent;

namespace Rowlan.SceneMark
{
    public class QuickNavDataManager
    {
        [SerializeField]
        public QuickNavData quickNavData;

        private SerializedObject serializedObject;

        public void OnEnable()
        {
            ScriptableObjectManager<QuickNavData> settingsManager = new ScriptableObjectManager<QuickNavData>(ProjectSetup.SETTINGS_FOLDER, ProjectSetup.SETTINGS_FILENAME);

            settingsManager.EnsureAssetPathExists(ProjectSetup.SETTINGS_SNAPSHOTS_FOLDER);

            quickNavData = settingsManager.GetAsset();

            serializedObject = new SerializedObject(quickNavData);
        }

        public void InitializeHistory()
        {
            // clear history at startup
            quickNavData.ClearHistory();

            EditorUtility.SetDirty(quickNavData);
        }

        public void Refresh()
        {
            quickNavData.Refresh();
        }

        public SerializedObject GetSerializedObject()
        {
            return serializedObject;
        }

        public void AddToFavorites(UnityEngine.Object[] objects)
        {
            if (objects == null)
                return;

            foreach (UnityEngine.Object unityObject in objects)
            {
                // add the selected object
                // check if the object is null; would eg be the case of the scene object in the hierarchy
                if (unityObject != null)
                {
                    QuickNavItem navItem = CreateQuickNavItem(unityObject);

                    quickNavData.AddFavoritesItem(navItem);
                }
            }

            // persist the changes
            EditorUtility.SetDirty(quickNavData);
        }

        public void AddToFavorites(QuickNavItem quickNavItem)
        {
            quickNavData.AddFavoritesItem(quickNavItem);

            EditorUtility.SetDirty(quickNavData);
        }

        public void AddSeparatorToFavorites()
        {
            quickNavData.AddSeparatorToFavorites();

            EditorUtility.SetDirty(quickNavData);
        }

        public QuickNavItem CreateQuickNavItem(UnityEngine.Object unityObject)
        {
            string guid;
            long localId;

            bool isProjectContext = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(unityObject, out guid, out localId);

            QuickNavItem navItem = new QuickNavItem(unityObject, isProjectContext);

            return navItem;
        }


        public SerializedProperty GetListProperty( ModuleType moduleType)
        {
            switch (moduleType)
            {
                case ModuleType.History:
                    return serializedObject.FindProperty("history").FindPropertyRelative("list"); // history: might need the "quickNavData." prefix depending on what is the parent

                case ModuleType.Favorites:
                    return serializedObject.FindProperty("favorites").FindPropertyRelative("list"); // favorites: might need the "quickNavData." prefix depending on what is the parent

                default: throw new System.Exception("Unsupported module type " + moduleType);
            }
        }

        public QuickNavList GetQuickNavList(ModuleType moduleType)
        {
            switch (moduleType)
            {
                case ModuleType.History:
                    return GetHistoryList();

                case ModuleType.Favorites:
                    return GetFavoritesList();

                default: throw new System.Exception("Unsupported module type " + moduleType);
            }
        }

        public QuickNavList GetHistoryList()
        {
            return quickNavData.history;
        }

        public QuickNavList GetFavoritesList()
        {
            return quickNavData.favorites;
        }

        public void Clear(ModuleType moduleType)
        {
            switch (moduleType)
            {
                case ModuleType.History:
                    quickNavData.ClearHistory();
                    break;

                case ModuleType.Favorites:
                    quickNavData.ClearFavorites();
                    break;
            }
        }

        public bool IsFavoritesItem(Vector3 sceneViewPosition)
        {
            return quickNavData.IsFavoritesItem(sceneViewPosition);
        }

        #region Selection related

        public void AddSelectedToFavorites()
        {
            AddToFavorites(Selection.objects);
        }

        public void AddCurrentSceneViewToFavorites()
        {
            ZoomEvent zoomEvent = SceneNav.SceneNav.Snapshot();
            QuickNavItem item = new QuickNavItem(zoomEvent);

            AddToFavorites(item);
        }

        /// <summary>
        /// Add to history if a single item got selected
        /// </summary>
        public void AddSelectedToHistory()
        {
            // single item selection / navigation
            if (Selection.objects.Length != 1)
                return;

            // add to history if a single item got selected
            UnityEngine.Object selectedUnityObject = Selection.objects[0];

            InsertHistoryItem(selectedUnityObject);
        }

        #endregion Selection related

        #region SceneNav related

        public void AddToHistory( ZoomEvent zoomEvent)
        {
            // TODO: check if the event is already in the history

            // Debug.Log($"zoomType: {zoomEvent.zoomType}, position: {zoomEvent.cameraPosition}, rotation: {zoomEvent.cameraRotation}, target: {zoomEvent.target}");

            if (zoomEvent.zoomType == ZoomType.Stop)
            {
                // add history object
                QuickNavItem item = new QuickNavItem(zoomEvent); 

                InsertHistoryItem(item);
            }
        }

        public void InsertHistoryItem(UnityEngine.Object unityObject)
        {
            // add the selected object to history
            // check if the object is null; would eg be the case of the scene object in the hierarchy
            if (unityObject == null)
                return;

            QuickNavItem quickNavItem = CreateQuickNavItem(unityObject);

            InsertHistoryItem(quickNavItem);

        }

        public void InsertHistoryItem(QuickNavItem quickNavItem)
        {
            quickNavData.InsertHistoryItem(quickNavItem);

            EditorUtility.SetDirty(quickNavData);
        }

        #endregion SceneNav related
    }
}