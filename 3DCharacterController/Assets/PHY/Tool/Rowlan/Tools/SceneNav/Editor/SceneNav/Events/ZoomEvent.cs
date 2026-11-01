using Rowlan.SceneMark;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rowlan.SceneNav
{
    public class ZoomEvent
    {
        public enum ZoomType
        {
            Start,
            Stop,
            Cancel
        }

        public ZoomType zoomType;
        
        public Vector3 cameraPosition;
        public Quaternion cameraRotation;
        
        public Vector3 sceneViewPosition;
        public Quaternion sceneViewRotation;
        public float sceneViewSize;

        public GameObject target;
        public Texture2D snapshot;

        private ZoomEvent()
        {
        }

        public ZoomEvent(ZoomType zoomType, Vector3 cameraPosition, Quaternion cameraRotation, Vector3 sceneViewPosition, Quaternion sceneViewRotation, float sceneViewSize, GameObject target, Texture2D snapshot)
        {
            this.zoomType = zoomType;
            
            this.cameraPosition = cameraPosition;
            this.cameraRotation = cameraRotation;
            
            this.sceneViewPosition = sceneViewPosition;
            this.sceneViewRotation = sceneViewRotation;
            this.sceneViewSize = sceneViewSize;

            this.target = target;

            this.snapshot = snapshot == null ? null : TextureUtils.CopyTexture2D(snapshot, true);
            ;
        }
    }
}