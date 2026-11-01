using System;
using UnityEngine;

namespace Rowlan.SceneNav
{
    [Serializable]
    public class SceneNavPreset
    {
        public enum PreviewPosition
        {
            Auto,
            SceneViewTopLeft,
            CursorCenter,
            CursorTopLeft,
            CursorTopRight,
            CursorBottomLeft,
            CursorBottomRight,
        }

        public string name = "Preset #";

        [Tooltip("Visibility of the target gameobject's name")]
        public bool labelVisible = true;

        [Tooltip("Zoom out in forward direction. Value should be negative to move the scene view camera backwards")]
        public float zoomOutDistance = -100f;

        [Tooltip("The position of the preview image")]
        public PreviewPosition previewPosition = PreviewPosition.CursorCenter;

        [Tooltip("The offset of the camera from the target object. Value should be negative")]
        public float cameraOffset = -50f;

        [Tooltip("The size of the preview image in pixels")]
        public int renderTextureSize = 200;

    }
}