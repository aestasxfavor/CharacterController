using Rowlan.SceneNav;
using System;
using UnityEditor;
using UnityEngine;

namespace Rowlan.SceneMark
{
    [Serializable]
    public class QuickNavItem
    {
        public static QuickNavItem CreateSeparator()
        {
            QuickNavItem item = new QuickNavItem(null, false);

            item.context = Context.Separator;
            item.title = "<Separator>";

            return item;
        }

        public enum Context
        {
            Scene,
            Separator
        }

        /// <summary>
        /// Whether the selection came from the scene or the project
        /// </summary>
        public Context context;

        /// <summary>
        /// Associated text to this item.
        /// For separators this is the separator title.
        /// For other context this is arbitrary text.
        /// </summary>
        public string title;

        /// <summary>
        /// Associated text to this item.
        /// For separators this is the separator title.
        /// For other context this is arbitrary text.
        /// </summary>
        public string details;

        /// <summary>
        /// The selection instance id
        /// </summary>
        //public int instanceId;

        /// <summary>
        /// The name of the selected object
        /// </summary>
        public UnityEngine.Object unityObject;

        /// <summary>
        /// the 
        /// </summary>
        public string objectGuid;

        #region SceneMark

        /// <summary>
        /// Position, rotation, texture, etc
        /// </summary>
        public SceneMark sceneMark;

        private QuickNavItem()
        {
        }

        public QuickNavItem(ZoomEvent zoomEvent)
        {
            this.unityObject = zoomEvent.target;
            this.context = Context.Scene;

            GlobalObjectId globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(unityObject);
            objectGuid = globalObjectId.ToString();

            sceneMark = new SceneMark(zoomEvent); // TODO: remove target gameobject; // TODO: don't use zoom-out position

        }

        public QuickNavItem Clone()
        {
            QuickNavItem clone = new QuickNavItem();

            clone.context = context;
            clone.title = title;
            clone.unityObject = unityObject;
            clone.objectGuid = objectGuid;

            // clone the scenemark object including the texture
            clone.sceneMark = sceneMark.Clone();

            return clone;
        }

        #endregion SceneMark

        public QuickNavItem(UnityEngine.Object unityObject, bool isProjectContext)
        {
            this.unityObject = unityObject;
            this.context = Context.Scene;

            GlobalObjectId globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(unityObject);
            objectGuid = globalObjectId.ToString();
        }

        /// <summary>
        /// Get the unity object using the object guid and assign it.
        /// This may become necessary e. g. after a restart of the unity editor.
        /// In that case the unity object could be lost, but using the object guid we can restore it.
        /// </summary>
        public void Refresh()
        {
            if (objectGuid == null)
                return;

            if (context == Context.Separator)
                return;

            GlobalObjectId id;
            if (!GlobalObjectId.TryParse( objectGuid, out id))
            {
                // Debug.Log("obj is null for " + objectGuid);
                return;
            }

            UnityEngine.Object parsedObject = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);

            if (parsedObject == null)
                return;

            unityObject = parsedObject;
        }
    }
}