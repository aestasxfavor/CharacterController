using Rowlan.SceneMark;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using static Rowlan.SceneNav.SceneNavPreset;
using static Rowlan.SceneNav.ZoomEvent;

namespace Rowlan.SceneNav
{
    /// <summary>
    /// Press hotkey to zoom out
    /// Release hotkey to zoom to focused position
    /// Right mouse button while zooming to cancel and return to original position
    /// </summary>
    [InitializeOnLoad]
    [ExecuteInEditMode]
    public class SceneNav
    {
        [SerializeField]
        public static SceneNavData SceneNavData;

        static SceneNav()
        {
            // data are loaded in asset post processor
        }

        /// <summary>
        /// Load the SceneNav Data
        /// Data are loaded in asset post processor. mustn't be loaded at startup, otherwise a race condition may occur when the asset gets imported from the asset store for the first time
        /// see also https://issuetracker-mig.prd.it.unity3d.com/issues/unable-to-import-newly-created-asset-errors-are-logged-when-creating-a-material-from-initializeonload
        /// </summary>
        public static void LoadData()
        {
            if (SceneNavData != null)
                return;

            ScriptableObjectManager<SceneNavData> settingsManager = new ScriptableObjectManager<SceneNavData>(ProjectSetup.SETTINGS_FOLDER, ProjectSetup.SETTINGS_FILENAME);
            SceneNavData = settingsManager.GetAsset();
        }

        [ClutchShortcutAttribute(PresetDefaults.PRESET_1_SHORTCUT_ID, typeof(SceneView), PresetDefaults.PRESET_1_SHORTCUT_KEY, PresetDefaults.PRESET_1_SHORTCUT_MODIFIER)]
        public static void SceneZoomShortcutPreset1(ShortcutArguments args)
        {
            if (!SceneNavData.assetEnabled)
                return;

            int presetIndex = 0; // index in preset list

            ApplyShortcut( args, presetIndex);
        }

        [ClutchShortcutAttribute(PresetDefaults.PRESET_2_SHORTCUT_ID, typeof(SceneView), PresetDefaults.PRESET_2_SHORTCUT_KEY, PresetDefaults.PRESET_2_SHORTCUT_MODIFIER)]
        public static void SceneZoomShortcutPreset2(ShortcutArguments args)
        {
            if (!SceneNavData.assetEnabled)
                return;

            int presetIndex = 1; // index in preset list

            ApplyShortcut(args, presetIndex);
        }

        [ClutchShortcutAttribute(PresetDefaults.PRESET_3_SHORTCUT_ID, typeof(SceneView), PresetDefaults.PRESET_3_SHORTCUT_KEY, PresetDefaults.PRESET_3_SHORTCUT_MODIFIER)]
        public static void SceneZoomShortcutPreset3(ShortcutArguments args)
        {
            if (!SceneNavData.assetEnabled)
                return;

            int presetIndex = 2; // index in preset list

            ApplyShortcut(args, presetIndex);
        }

        private static void ApplyShortcut(ShortcutArguments args, int presetIndex)
        {
            // check preset list index
            if( !PresetDefaults.IsInPresetDefaultsRange( presetIndex))
            {
                Debug.LogError("Preset index out of range: " + presetIndex);
                return;
            }

            _currentPreset = SceneNavData.presets[ presetIndex];

            if (args.stage == ShortcutStage.Begin && zoomStage == ZoomStage.None)
            {
                StartZoom(_currentPreset);
            }
            else if (args.stage == ShortcutStage.End && zoomStage == ZoomStage.Out)
            {
                StopZoom();
            }

        }

        private enum ZoomStage
        {
            None,
            Out,
        }

        private enum ZoomAction
        {
            Apply,
            Cancel,
        }

        private static SceneNavPreset _currentPreset;

        public static SceneNavPreset CurrentPreset
        {
            get
            {
                if (_currentPreset == null)
                {
                    _currentPreset = PresetDefaults.Preset1;
                }


                return _currentPreset;
            }
        }

        private static ZoomStage zoomStage = ZoomStage.None;
        
        private static Vector3 originalCameraPosition = Vector3.zero;
        private static Quaternion originalCameraRotation = Quaternion.identity;

        private static Vector3 originalSceneViewPosition = Vector3.zero;
        private static Quaternion originalSceneViewRotation = Quaternion.identity;
        private static float originalSceneViewSize = 10f; // just some number, will be overwritten. could use 0, but let's play it safe

        private static SceneNavCamera sceneNavCamera;
        private static ZoomAction zoomAction = ZoomAction.Apply;
        private static bool isZoomActive = false;

        private static void StartZoom(SceneNavPreset preset)
        {
            SceneView.lastActiveSceneView.Focus();

            zoomStage = ZoomStage.Out;
            zoomAction = ZoomAction.Apply;

            originalCameraPosition = SceneView.lastActiveSceneView.camera.transform.position;
            originalCameraRotation = SceneView.lastActiveSceneView.camera.transform.rotation;

            originalSceneViewPosition = SceneView.lastActiveSceneView.pivot;
            originalSceneViewRotation = SceneView.lastActiveSceneView.rotation;
            originalSceneViewSize = SceneView.lastActiveSceneView.size;

            Vector3 position = SceneView.lastActiveSceneView.camera.transform.position + SceneView.lastActiveSceneView.camera.transform.forward * preset.zoomOutDistance;

            SetSceneViewPosition(position);

            OnSceneEnabled();

            isZoomActive = true;

            ZoomEvent zoomEvent = new ZoomEvent(ZoomType.Start, originalCameraPosition, originalCameraRotation, originalSceneViewPosition, originalSceneViewRotation, originalSceneViewSize, null, null);

            SceneNavEventManager.FireZoomEvent(zoomEvent);
        }

        private static void StopZoom()
        {
            if (!isZoomActive)
                return;

            zoomStage = ZoomStage.None;

            ZoomEvent zoomEvent = null;
            Texture2D snapshot = null;

            if (zoomAction == ZoomAction.Cancel)
            {
                SetSceneViewPosition(originalCameraPosition);

                zoomEvent = new ZoomEvent(ZoomType.Cancel, originalCameraPosition, originalCameraRotation, originalSceneViewPosition, originalSceneViewRotation, originalSceneViewSize, null, null);

            }
            else if (zoomAction == ZoomAction.Apply)
            {
                Vector3 position = sceneNavCamera.GetFocusPosition();
                
                SetSceneViewPosition(position);

                // calculate the pivot manually
                // SceneView.lastActiveSceneView.pivot isn't available yet; the camera would be offset when you navigate in history & favorites
                Vector3 sceneViewPosition = position + sceneNavCamera.GetTransform().forward * SceneView.lastActiveSceneView.cameraDistance;
                Quaternion sceneViewRotation = SceneView.lastActiveSceneView.rotation;
                float sceneViewSize = SceneView.lastActiveSceneView.size;

                snapshot = sceneNavCamera.hasTarget ? TextureUtils.CreateTexture2D(sceneNavCamera.GetRenderTexture()) : null;
                zoomEvent = new ZoomEvent( //
                    ZoomType.Stop, //
                    sceneNavCamera.GetFocusPosition(), //
                    sceneNavCamera.GetFocusRotation(), //
                    sceneViewPosition, //
                    sceneViewRotation, //
                    sceneViewSize, //
                    sceneNavCamera.hasTarget ? sceneNavCamera.targetGameObject : null, //
                    snapshot //
                    );
            }

            if(zoomEvent != null)
            {
                SceneNavEventManager.FireZoomEvent(zoomEvent);
            }

            if (snapshot != null)
            {
                UnityEngine.Object.DestroyImmediate(snapshot);
            }

            OnSceneDisabled();

            isZoomActive = false;
        }

        private static bool IsZoomActive()
        {
            return isZoomActive;
        }

        private static void SetSceneViewPosition(Vector3 position)
        {
            SceneView.lastActiveSceneView.camera.transform.position = position;

            SceneView.lastActiveSceneView.AlignViewToObject(SceneView.lastActiveSceneView.camera.transform);
            SceneView.lastActiveSceneView.Repaint();

        }

        public static void OnSceneEnabled()
        {
            sceneNavCamera = new SceneNavCamera();
            sceneNavCamera.OnEnable();

            SceneView.duringSceneGui -= OnScene;
            SceneView.duringSceneGui += OnScene;

            SceneNavEventManager.OnZoom -= OnZoom;
            SceneNavEventManager.OnZoom += OnZoom;

        }

        public static void OnSceneDisabled()
        {
            SceneNavEventManager.OnZoom -= OnZoom;

            SceneView.duringSceneGui -= OnScene;

            sceneNavCamera.OnDisable();
            sceneNavCamera = null;

        }

        public static void OnZoom(ZoomEvent zoomEvent)
        {
            // Debug.Log( $"zoomType: {zoomType}, position: {position}");
        }

        private static void OnScene(SceneView sceneview)
        {
            if (Event.current.type == EventType.MouseUp)
            {
                if (Event.current.button == 1)
                {
                    CancelZoom();
                }
            }

            // repaint the sceneview when the mouse is moved while the hotkey is pressed
            // this works with out for eg shift+tab, but not for shift+alpha1 or other keys
            if (Event.current.type == EventType.MouseMove)
            {
                if(IsZoomActive())
                {
                    SceneView.lastActiveSceneView.Repaint();
                }
            }

            if (Event.current.type == EventType.Repaint)
            {
                sceneNavCamera.OnSceneGUI();

                DrawTexture(sceneNavCamera.GetRenderTexture());
            }
        }

        private static void CancelZoom()
        {
            zoomAction = ZoomAction.Cancel;

            StopZoom();
        }

        private static void DrawTexture( RenderTexture renderTexture)
        {
            // show target texture only if there's a target found
            if (!sceneNavCamera.hasTarget)
                return;

            Handles.BeginGUI();

            // draw the preview image
            Rect rect = GetPreviewImageRect( renderTexture);
            GUI.DrawTexture(rect, renderTexture, ScaleMode.ScaleToFit);

            // draw the target gameobject
            if (CurrentPreset.labelVisible)
            {
                GUI.Label(rect, sceneNavCamera.targetGameObject.name, GUIStyles.PreviewLabelStyle);
            }

            Handles.EndGUI();
        }

        private static Rect GetPreviewImageRect(RenderTexture renderTexture)
        {
            int padding = 10;
            int size = renderTexture.width;

            Vector3 mousePosition = Event.current.mousePosition;

            PreviewPosition previewPosition = CurrentPreset.previewPosition;

            // auto position: set the preview position to a concrete one
            if(previewPosition == PreviewPosition.Auto)
            {
                previewPosition = GetAutoPreviewPosition( mousePosition, size, padding);
            }

            float x;
            float y;

            switch (previewPosition)
            {
                case PreviewPosition.SceneViewTopLeft: 
                    x = padding; 
                    y = padding; 
                    break;

                case PreviewPosition.CursorCenter: 
                    x = mousePosition.x - size / 2; 
                    y = mousePosition.y - size / 2;
                    break;

                case PreviewPosition.CursorTopLeft: 
                    x = mousePosition.x - size - padding; 
                    y = mousePosition.y - size - padding; 
                    break;

                case PreviewPosition.CursorTopRight:
                    x = mousePosition.x + padding; 
                    y = mousePosition.y - size - padding;
                    break;

                case PreviewPosition.CursorBottomLeft: 
                    x = mousePosition.x - size - padding; 
                    y = mousePosition.y + padding; 
                    break;

                case PreviewPosition.CursorBottomRight: 
                    x = mousePosition.x + padding; 
                    y = mousePosition.y + padding; 
                    break;

                default: throw new ArgumentException("Unsupported preview position: " + CurrentPreset.previewPosition);
            }

            Rect rect = new Rect(x, y, size, size);

            return rect;
        }

        /// <summary>
        /// Auto position: default is top left; is sceneview window bounds are reached, then the preview position is adapted accordingly
        /// </summary>
        /// <param name="mousePosition"></param>
        /// <param name="size"></param>
        /// <param name="padding"></param>
        /// <returns></returns>
        private static PreviewPosition GetAutoPreviewPosition( Vector3 mousePosition, int size, int padding)
        {
            // default
            PreviewPosition previewPosition = PreviewPosition.CursorTopLeft;

            // dimensions of the sceneview
            Rect windowRect = SceneView.lastActiveSceneView.position;

            // distance at which we make the decision to switch
            // full size is too much, we like to have a little clipping before we switch
            float sizeLimit = size / 2f;

            // calculate limits
            bool leftLimit = mousePosition.x - padding - sizeLimit < 0;
            bool topLimit = mousePosition.y - padding - sizeLimit < 0;
            bool rightLimit = mousePosition.x - padding + sizeLimit > windowRect.x + windowRect.width;
            bool bottomLimit = mousePosition.y - padding + sizeLimit > windowRect.y + windowRect.height;

            // switch decision
            bool anyLimit = leftLimit || topLimit || rightLimit || bottomLimit;

            if (anyLimit)
            {
                // top left corner
                if (leftLimit && topLimit)
                    previewPosition = PreviewPosition.CursorBottomRight;
                else if (leftLimit && !topLimit)
                    previewPosition = PreviewPosition.CursorTopRight;
                else if (!leftLimit && topLimit)
                    previewPosition = PreviewPosition.CursorBottomLeft;

                // top right corner
                else if (rightLimit && topLimit)
                    previewPosition = PreviewPosition.CursorBottomLeft;
                else if (rightLimit && !topLimit)
                    previewPosition = PreviewPosition.CursorTopLeft;
                else if (!rightLimit && topLimit)
                    previewPosition = PreviewPosition.CursorBottomLeft;

                // bottom left corner
                else if (leftLimit && bottomLimit)
                    previewPosition = PreviewPosition.CursorTopLeft;
            }

            return previewPosition;
        }

        /// <summary>
        /// Create a ZoomEvent with a capture of the current sceneview
        /// </summary>
        /// <returns></returns>
        public static ZoomEvent Snapshot()
        {
            Texture2D snapshot = SceneNavCamera.CaptureCurrentSceneView();

            // create zoomevent
            Vector3 cameraPosition = SceneView.lastActiveSceneView.camera.transform.position;
            Quaternion cameraRotation = SceneView.lastActiveSceneView.camera.transform.rotation;

            Vector3 sceneViewPosition = SceneView.lastActiveSceneView.pivot;
            Quaternion sceneViewRotation = SceneView.lastActiveSceneView.rotation;
            float sceneViewSize = SceneView.lastActiveSceneView.size;

            ZoomEvent zoomEvent = new ZoomEvent(ZoomType.Stop, cameraPosition, cameraRotation, sceneViewPosition, sceneViewRotation, sceneViewSize, null, snapshot);

            UnityEngine.Object.DestroyImmediate(snapshot);

            return zoomEvent;
        }
    }

}