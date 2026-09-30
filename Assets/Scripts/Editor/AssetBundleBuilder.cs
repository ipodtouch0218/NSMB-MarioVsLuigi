using UnityEditor;
using UnityEngine;

namespace NSMB.Editor {
    [InitializeOnLoad]
    public class AssetBundleBuilder {
        static AssetBundleBuilder() {
            BuildPlayerWindow.RegisterBuildPlayerHandler(buildPlayerOptions => {
                BuildAssetBundles(buildPlayerOptions.target, BuildAssetBundleOptions.AssetBundleStripUnityVersion | BuildAssetBundleOptions.ChunkBasedCompression);
                BuildPlayerWindow.DefaultBuildMethods.BuildPlayer(buildPlayerOptions);
            });

            EditorApplication.playModeStateChanged += (state) => {
                if (state == PlayModeStateChange.ExitingEditMode) {
                    BuildAssetBundles(EditorUserBuildSettings.activeBuildTarget, BuildAssetBundleOptions.AssetBundleStripUnityVersion | BuildAssetBundleOptions.UncompressedAssetBundle);
                }
            };
        }

        public static void BuildAssetBundles(BuildTarget buildTarget, BuildAssetBundleOptions options) {
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
            
            BuildPipeline.BuildAssetBundles(Application.streamingAssetsPath, buildMap, options, buildTarget);
        }
    }
}
