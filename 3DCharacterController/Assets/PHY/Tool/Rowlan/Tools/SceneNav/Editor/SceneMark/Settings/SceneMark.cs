using Rowlan.SceneNav;
using System;
using UnityEngine;

namespace Rowlan.SceneMark
{
    [Serializable]
    public class SceneMark
    {
        public Vector3 cameraPosition;
        public Quaternion cameraRotation;

        /// <summary>
        /// Scene view data.
        /// Note that size needs to be considered.
        /// Example: If you are close to an object, then double-click terrain to zoom out, the size changes in Unity.
        /// If it weren't considered, the close object wouldn't be zoomed to, but viewed from a distance.
        /// </summary>
        public Vector3 sceneViewPosition;
        public Quaternion sceneViewRotation;
        public float sceneViewSize;

        public GameObject target;

        [SerializeField]
        public Texture2D snapshot;

        private SceneMark()
        {
        }

        public SceneMark(ZoomEvent zoomEvent)
        {
            this.cameraPosition = zoomEvent.cameraPosition;
            this.cameraRotation = zoomEvent.cameraRotation;

            this.sceneViewPosition = zoomEvent.sceneViewPosition;
            this.sceneViewRotation = zoomEvent.sceneViewRotation;
            this.sceneViewSize = zoomEvent.sceneViewSize;

            this.target = zoomEvent.target;
            this.snapshot = zoomEvent.snapshot;
        }

        public SceneMark Clone()
        {
            SceneMark clone = new SceneMark();

            clone.cameraPosition = cameraPosition;
            clone.cameraRotation = cameraRotation;

            clone.sceneViewPosition = sceneViewPosition;
            clone.sceneViewRotation = sceneViewRotation;
            clone.sceneViewSize = sceneViewSize;

            clone.target = target;
            clone.snapshot = TextureUtils.CopyTexture2D(snapshot, true);

            return clone;

        }

    }
}