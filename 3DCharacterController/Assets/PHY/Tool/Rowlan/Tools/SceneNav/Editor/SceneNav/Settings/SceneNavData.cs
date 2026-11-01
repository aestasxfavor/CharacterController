using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rowlan.SceneNav
{
    [Serializable]
    public class SceneNavData : ScriptableObject
    {
        /// <summary>
        /// Show help box for information
        /// </summary>
        public bool helpBoxVisible = false;

        /// <summary>
        /// Whether SceneNav is enabled or not
        /// </summary>
        public bool assetEnabled = true;

        /// <summary>
        /// A pre-configured list of presets. Adjustable in the editor
        /// </summary>
        public List<SceneNavPreset> presets;

        public void Reset()
        {
            assetEnabled = true;

            presets = PresetDefaults.CreatePresetDefaults();
        }
    }
}