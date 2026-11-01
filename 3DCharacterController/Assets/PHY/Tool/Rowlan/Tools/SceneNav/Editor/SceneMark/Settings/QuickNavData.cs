using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rowlan.SceneMark
{
    [Serializable]
    public class QuickNavData : ScriptableObject
    {
        /// <summary>
        /// Maximum number of items in the history list
        /// </summary>
        public int historyItemsMax = 10;

        /// <summary>
        /// The history list
        /// </summary>
        public QuickNavList history;

        /// <summary>
        /// The favorites list
        /// </summary>
        public QuickNavList favorites;

        /// <summary>
        /// Check if the favorites list contains the specified item.
        /// Used e. g. to find out if a history item is already in the favorites list.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool IsFavoritesItem( Vector3 sceneViewPosition)
        {
            foreach( QuickNavItem currentItem in favorites.GetList())
            {
                if (currentItem.sceneMark.sceneViewPosition == sceneViewPosition)
                    return true;
            }

            return false;
        }

        public void Reset()
        {
            // remove textures
            history.Clear();
            favorites.Clear();

            // Debug.Log("Reset");

            history = new QuickNavList();
            history.Reset(); // required for .Net 2.1, all versions above 9 don't need it

            favorites = new QuickNavList();
            favorites.Reset(); // required for .Net 2.1, all versions above 9 don't need it
        }

        public void OnValidate()
        {
            // Debug.Log( "OnValidate " + Time.time);
        }

        /// <summary>
        /// Get the unity object using the object guid and assign it.
        /// This may become necessary e. g. after a restart of the unity editor.
        /// In that case the unity object could be lost, but using the object guid we can restore it.
        /// </summary>
        public void Refresh()
        {
            history.Refresh();
            favorites.Refresh();
        }

        public void InsertHistoryItem(QuickNavItem quickNavItem)
        {
            // ensure collection doesn't exceed max size
            ClampHistorySize();

            history.Insert(0, quickNavItem);
        }

        public void AddFavoritesItem(QuickNavItem quickNavItem)
        {
            favorites.Add(quickNavItem);
        }

        public void AddSeparatorToFavorites()
        {
            QuickNavItem separator = QuickNavItem.CreateSeparator();

            favorites.Add(separator);

        }

        public void ClampHistorySize()
        {
            history.Clamp(historyItemsMax);
        }

        public void ClearHistory()
        {
            history.Clear();
        }

        public void ClearFavorites()
        {
            favorites.Clear();
        }
    }
}
