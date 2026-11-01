using System.Collections;
using System.Collections.Generic;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using static Rowlan.SceneNav.SceneNavPreset;

namespace Rowlan.SceneNav
{
    public class PresetDefaults
    {

        #region Shortcuts

        // shortcut preset 1
        public const string PRESET_1_SHORTCUT_ID = "Rowlan/SceneNav/Preset1";
        public const KeyCode PRESET_1_SHORTCUT_KEY = KeyCode.Alpha1;
        public const ShortcutModifiers PRESET_1_SHORTCUT_MODIFIER = ShortcutModifiers.Shift;

        // shortcut preset 2
        public const string PRESET_2_SHORTCUT_ID = "Rowlan/SceneNav/Preset2";
        public const KeyCode PRESET_2_SHORTCUT_KEY = KeyCode.Alpha2;
        public const ShortcutModifiers PRESET_2_SHORTCUT_MODIFIER = ShortcutModifiers.Shift;

        // shortcut preset 3
        public const string PRESET_3_SHORTCUT_ID = "Rowlan/SceneNav/Preset3";
        public const KeyCode PRESET_3_SHORTCUT_KEY = KeyCode.Alpha3;
        public const ShortcutModifiers PRESET_3_SHORTCUT_MODIFIER = ShortcutModifiers.Shift;

        /// <summary>
        /// List of the used shortcut ids. Those differ from the list of presets.
        /// It should be possible to drag presets in the reorderable list and have the shortcut according to this list assigned.
        /// Example: Move preset 3 up to slot 1, then the shortcut changes from 3 to 1.
        /// </summary>
        public static string[] PresetShortcutIds = new string[] { 
            PRESET_1_SHORTCUT_ID, 
            PRESET_2_SHORTCUT_ID, 
            PRESET_3_SHORTCUT_ID, 
        };

        #endregion Shortcuts

        #region Presets

        public static SceneNavPreset Preset1 = new SceneNavPreset()
        {
            name = "Preset 1",
            previewPosition = PreviewPosition.Auto,
            zoomOutDistance = -50,
            cameraOffset = -10f
        };

        public static SceneNavPreset Preset2 = new SceneNavPreset()
        {
            name = "Preset 2",
            previewPosition = PreviewPosition.Auto,
            zoomOutDistance = -100,
            cameraOffset = -15f
        };

        public static SceneNavPreset Preset3 = new SceneNavPreset()
        {
            name = "Preset 3",
            previewPosition = PreviewPosition.Auto,
            zoomOutDistance = -150,
            cameraOffset = -15f
        };

        public static SceneNavPreset[] PresetDefaultList = new SceneNavPreset[] {
            Preset1,
            Preset2,
            Preset3,
        };

        public static List<SceneNavPreset> CreatePresetDefaults()
        {
            List<SceneNavPreset> presets = new List<SceneNavPreset>();

            presets.AddRange(PresetDefaultList);

            return presets;
        }

        public static bool IsInPresetDefaultsRange( int index)
        {
            return index >= 0 && index < PresetDefaultList.Length;
        }

        #endregion Presets
    }
}