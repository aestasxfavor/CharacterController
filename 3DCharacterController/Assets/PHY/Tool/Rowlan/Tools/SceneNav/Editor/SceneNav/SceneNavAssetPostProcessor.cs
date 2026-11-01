using UnityEditor;

namespace Rowlan.SceneNav
{
    /// <summary>
    /// Load the SceneNav Data in an asset postprocessing step. Can't load them in InitializeOnLoad
    /// </summary>
    public class SceneNavAssetPostProcessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload)
        {
            if (didDomainReload)
            {
                SceneNav.LoadData();
            }
        }
    }
}