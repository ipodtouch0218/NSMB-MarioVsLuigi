using UnityEditor;
using UnityEngine;

namespace NSMB.Editor {
    [InitializeOnLoad]
    public class AssetBundleBuilder {
        static AssetBundleBuilder() {
            BuildPlayerWindow.RegisterBuildPlayerHandler(buildPlayerOptions => {
                BuildAssetBundles(buildPlayerOptions);
                BuildPlayerWindow.DefaultBuildMethods.BuildPlayer(buildPlayerOptions);
            });
        }

        public static void BuildAssetBundles(BuildPlayerOptions buildOptions) {
            AssetBundleBuild[] buildMap = {
                new() {
                    assetBundleName = "basegame-assets",
                    assetNames = AssetDatabase.GetAssetPathsFromAssetBundle("basegame-assets"),
                },
                new() {
                    assetBundleName = "basegame-scenes",
                    assetNames = AssetDatabase.GetAssetPathsFromAssetBundle("basegame-scenes"),
                }
            };
            
            BuildPipeline.BuildAssetBundles(
                Application.streamingAssetsPath,
                buildMap,
                /*BuildAssetBundleOptions.DisableWriteTypeTree | */ BuildAssetBundleOptions.AssetBundleStripUnityVersion | BuildAssetBundleOptions.ChunkBasedCompression,
                buildOptions.target);
        }
    }
}
