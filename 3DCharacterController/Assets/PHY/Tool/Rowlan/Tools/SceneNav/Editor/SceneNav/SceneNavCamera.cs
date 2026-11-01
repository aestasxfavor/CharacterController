using Rowlan.SceneMark;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static Rowlan.SceneNav.ZoomEvent;

namespace Rowlan.SceneNav
{
    public class SceneNavCamera
    {
        private static bool useAspect = false;

        private bool isFocusing = false;

        private Vector3 focusPosition = Vector3.zero;
        private Quaternion focusRotation = Quaternion.identity;

        private Camera zoomCamera;

        private RenderTexture renderTexture = null;

        private GameObject editorTarget;

        /// <summary>
        /// Whether a target was found or not, resembles the raycast hit
        /// </summary>
        public bool hasTarget = false;

        /// <summary>
        /// The gameobject that was hit by the raycast
        /// </summary>
        public GameObject targetGameObject;

        public void OnEnable()
        {
            editorTarget = new GameObject();
            editorTarget.hideFlags = HideFlags.HideAndDontSave;

            StartFocusing();
        }

        public void OnDisable()
        {
            StopFocusing();

            Object.DestroyImmediate(editorTarget);
        }

        public void OnSceneGUI()
        {
            // no need to repaint all the time
            if (Event.current.type == EventType.Repaint)
            {
                DoFocusing();
            }

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                MoveSceneView();

                Event.current.Use();
            }
        }

        private void StartFocusing()
        {

            isFocusing = true;

            zoomCamera = editorTarget.gameObject.GetComponent<Camera>();
            if (!zoomCamera)
            {
                zoomCamera = editorTarget.gameObject.AddComponent<Camera>();
            }

            zoomCamera.CopyFrom(SceneView.lastActiveSceneView.camera);

            int width = SceneNav.CurrentPreset.renderTextureSize;
            int height = useAspect ? (int)(width / SceneView.lastActiveSceneView.camera.aspect) : width;

            renderTexture = RenderTexture.GetTemporary(width, height, 32);
            zoomCamera.targetTexture = renderTexture;

            zoomCamera.enabled = true;

        }

        private void DoFocusing()
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Camera sceneCam = SceneView.lastActiveSceneView.camera;

                Vector3 distance = hit.point - sceneCam.transform.position;
                Vector3 direction = distance.normalized;

                Vector3 offset = direction * SceneNav.CurrentPreset.cameraOffset;

                focusPosition = hit.point + offset;
                focusRotation = sceneCam.transform.rotation;

                zoomCamera.transform.position = focusPosition;
                zoomCamera.transform.rotation = focusRotation;

                hasTarget = true;
                targetGameObject = hit.transform.gameObject;

                // effectively update the camera
                zoomCamera.Render();

            }
            else
            {
                hasTarget = false;
                targetGameObject = null;
            }
        }

        private void MoveSceneView()
        {
            if (isFocusing)
            {
                SceneView.lastActiveSceneView.AlignViewToObject(zoomCamera.transform);
                SceneView.lastActiveSceneView.Repaint();
            }
        }

        private void StopFocusing()
        {
            if (zoomCamera)
            {
                zoomCamera.enabled = false;

                zoomCamera.targetTexture = null;
                zoomCamera = null;
                RenderTexture.ReleaseTemporary(renderTexture);
            }

            isFocusing = false;
        }

        public RenderTexture GetRenderTexture()
        {
            return renderTexture;
        }

        public Vector3 GetFocusPosition()
        {
            return focusPosition;
        }

        public Quaternion GetFocusRotation()
        {
            return focusRotation;
        }

        /// <summary>
        /// Create a snapshot of the current scenview using same settings as preview
        /// </summary>
        /// <returns></returns>
        public static Texture2D CaptureCurrentSceneView()
        {
            int width = SceneNav.CurrentPreset.renderTextureSize;
            int height = useAspect ? (int)(width / SceneView.lastActiveSceneView.camera.aspect) : width;

            Texture2D snapshot;

            RenderTexture renderTexture = RenderTexture.GetTemporary(width, height, 32);
            {
                RenderTexture prevRT = RenderTexture.active;
                {
                    RenderTexture.active = renderTexture;

                    // render scene to rendertexture
                    RenderTexture prevSceneRT = SceneView.lastActiveSceneView.camera.targetTexture;
                    {
                        SceneView.lastActiveSceneView.camera.targetTexture = renderTexture;

                        // render
                        SceneView.lastActiveSceneView.camera.Render();

                        // create texture2d
                        snapshot = TextureUtils.CreateTexture2D(renderTexture);
                    }
                    SceneView.lastActiveSceneView.camera.targetTexture = prevSceneRT;
                }
                RenderTexture.active = prevRT;
            }
            RenderTexture.ReleaseTemporary(renderTexture);

            return snapshot;
        }

        public Transform GetTransform()
        {
            return zoomCamera.transform;
        }
    }
}
