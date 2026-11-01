using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rowlan.SceneMark
{
    [System.Serializable]
    public struct QuickNavList 
    {
        [SerializeField]
        public List<QuickNavItem> list;

        public void Reset()
        {
            // remove old entries, ensure textures get removed
            Clear();

            list = new List<QuickNavItem>();
        }

        /// <summary>
        /// Remove items, ensure textures get removed
        /// </summary>
        public void Clear()
        {
            if (list == null)
                return;

            while( list.Count > 0)
            {
                RemoveItemAt(list.Count - 1);
            }
        }

        /// <summary>
        /// Wrapper for the Add method of List
        /// </summary>
        /// <param name="quickNavItem"></param>
        public void Add( QuickNavItem quickNavItem)
        {
            AddItem(quickNavItem);
        }

        /// <summary>
        /// Wrapper for the Insert method of List
        /// </summary>
        /// <param name="quickNavItem"></param>
        public void Insert(int index, QuickNavItem quickNavItem)
        {
            InsertItem(index, quickNavItem);
        }

        /// <summary>
        /// Wrapper for the RemoveAt method of List
        /// </summary>
        /// <param name="quickNavItem"></param>
        public void RemoveAt(int index)
        {
            RemoveItemAt(index);
        }

        /// <summary>
        /// Ensure collection doesn't exceed max size
        /// </summary>
        /// <param name="itemsMax"></param>
        public void Clamp( int itemsMax)
        {
            if (list == null)
                return;

            while (list.Count >= itemsMax)
            {
                RemoveItemAt(list.Count - 1);
            }
        }

        public List<QuickNavItem> GetList()
        {
            return list;
        }

        public int Count()
        {
            return list.Count;
        }

        public QuickNavItem GetItemAt( int index)
        {
            return list[index];
        }

        public void Refresh()
        {
            if (list == null)
                return;

            foreach( QuickNavItem quickNavItem in list)
            {
                quickNavItem.Refresh();
            }
        }

        #region Texture Handlers

        private void AddItem(QuickNavItem quickNavItem)
        {
            SaveTexture(quickNavItem);

            list.Add(quickNavItem);

        }

        private void InsertItem(int index, QuickNavItem quickNavItem)
        {
            SaveTexture(quickNavItem);

            list.Insert(index, quickNavItem);
        }

        private void RemoveItemAt(int index)
        {
            QuickNavItem quickNavItem = list[index];

            DeleteTexture(quickNavItem);

            list.RemoveAt(index);
        }

        private void SaveTexture(QuickNavItem quickNavItem)
        {
            Texture2D texture = quickNavItem?.sceneMark?.snapshot;

            if (texture == null)
                return;

            string path = ProjectSetup.SETTINGS_SNAPSHOTS_FOLDER + "/Snapshot.png";
            path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath(path);

            byte[] bytes = ImageConversion.EncodeToPNG(texture);

            System.IO.File.WriteAllBytes(path, bytes);

            Texture2D.DestroyImmediate(texture);
            texture = null;

            UnityEditor.AssetDatabase.Refresh();

            quickNavItem.sceneMark.snapshot = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private void DeleteTexture(QuickNavItem quickNavItem)
        {
            if (quickNavItem == null)
                return;

            if (quickNavItem.sceneMark == null)
                return;

            if (quickNavItem.sceneMark.snapshot == null)
                return;

            string path = UnityEditor.AssetDatabase.GetAssetPath(quickNavItem.sceneMark.snapshot);

            UnityEditor.AssetDatabase.DeleteAsset(path);
            
            UnityEditor.AssetDatabase.Refresh();
        }

        #endregion Texture Handlers
    }
}