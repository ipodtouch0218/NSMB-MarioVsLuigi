using JimmysUnityUtilities;
using Newtonsoft.Json;
using NSMB.Addons;
using NSMB.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NSMB.Editor {

    public class AddonBuildWindow : EditorWindow {

        private static readonly Dictionary<BuildTarget, string> BuildTargets = new() {
            [BuildTarget.StandaloneWindows] = "Win32",
            [BuildTarget.StandaloneOSX] = "MacOS",
            [BuildTarget.StandaloneLinux64] = "Linux",
            [BuildTarget.Android] = "Android",
            [BuildTarget.iOS] = "iOS",
            [BuildTarget.WebGL] = "WebGL",
        };

        public class BuildableAddon {
            public string FolderPath;
            public AddonDefinition EditorDefinition;
            public AddonBuildDefinition BuildDefinition;

            public string FolderName => new DirectoryInfo(FolderPath).Name;
        }

        private List<BuildableAddon> availableAddonFolders;
        private int selectedAddonFolder;
        private Vector2 addonFolderSelectScroll;


        [MenuItem("Tools/MvLO/Addons/Build", secondaryPriority = 2)]
        public static void BuildAddons() {
            GetWindow<AddonBuildWindow>();
        }

        public void OnEnable() {
            availableAddonFolders = new();
            foreach (var folderPath in Directory.GetDirectories("Assets/Addons")) {
                if (new DirectoryInfo(folderPath).Name.StartsWith('.')) {
                    continue;
                }

                List<AddonDefinition> addonDefinitions = AssetDatabase.FindAssets($"t:{nameof(AddonDefinition)}", new string[] { folderPath })
                    .Select(str => {
                        GUID.TryParse(str, out GUID result);
                        return result;
                    })
                    .Select(guid => AssetDatabase.LoadAssetByGUID<AddonDefinition>(guid))
                    .ToList();
                if (addonDefinitions.Count <= 0) {
                    Debug.LogWarning($"Unable to find any {nameof(AddonDefinition)}s inside the {folderPath} addon folder!");
                    continue;
                } else if (addonDefinitions.Count > 1) {
                    string names = string.Join(", ", addonDefinitions.Select(ad => ad.name));
                    Debug.LogWarning($"Found multiple {nameof(AddonDefinition)}s ({names}) inside the {folderPath} addon folder! Please only use one per addon folder!");
                    continue;
                }

                AddonDefinition editorDef = addonDefinitions[0];
                BuildableAddon buildableAddon = new BuildableAddon {
                    FolderPath = folderPath,
                    EditorDefinition = editorDef,
                    BuildDefinition = editorDef.ToBuildDefinition(Guid.NewGuid(), GameVersion.Parse(PlayerSettings.bundleVersion))
                };

                availableAddonFolders.Add(buildableAddon);
            }
        }

        public void OnDisable() {
            foreach (var addon in availableAddonFolders) {
                EditorUtility.SetDirty(addon.EditorDefinition);
            }
        }

        public void OnGUI() {
            if (availableAddonFolders.Count <= 0) {
                EditorGUILayout.LabelField("No addon folders exist!", EditorStyles.boldLabel);

                if (GUILayout.Button("Open Addon Create Window")) {
                    AddonCreateWindow.CreateAddon();
                    Close();
                }
                return;
            }

            EditorGUILayout.LabelField("Select a folder to build into an addon", EditorStyles.boldLabel);
            addonFolderSelectScroll = EditorGUILayout.BeginScrollView(addonFolderSelectScroll);
            selectedAddonFolder = GUILayout.SelectionGrid(selectedAddonFolder, availableAddonFolders.Select(ba => " " + ba.FolderName).ToArray(), 1, EditorStyles.radioButton);
            EditorGUILayout.EndScrollView();

            // Separator line
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            BuildableAddon selectedAddon = availableAddonFolders[selectedAddonFolder];

            selectedAddon.EditorDefinition.DisplayName = selectedAddon.BuildDefinition.DisplayName = EditorGUILayout.TextField("Display Name", selectedAddon.BuildDefinition.DisplayName);
            selectedAddon.EditorDefinition.Author = selectedAddon.BuildDefinition.Author = EditorGUILayout.TextField("Author(s)", selectedAddon.BuildDefinition.Author);
            selectedAddon.EditorDefinition.LastVersion = selectedAddon.BuildDefinition.ReleaseVersion = EditorGUILayout.TextField("Version", selectedAddon.BuildDefinition.ReleaseVersion);
            EditorStyles.textArea.wordWrap = true;
            EditorGUILayout.LabelField("Description");
            selectedAddon.EditorDefinition.Description = selectedAddon.BuildDefinition.Description = EditorGUILayout.TextArea(selectedAddon.BuildDefinition.Description, EditorStyles.textArea, GUILayout.Height(80));
            selectedAddon.EditorDefinition.IconTexture = selectedAddon.BuildDefinition.IconTexture = (Texture2D) EditorGUILayout.ObjectField("Icon", selectedAddon.BuildDefinition.IconTexture, typeof(Texture2D), false);

            if (GUILayout.Button("Build")) {
                if (string.IsNullOrWhiteSpace(selectedAddon.BuildDefinition.ReleaseVersion)) {
                    EditorUtility.DisplayDialog("Invalid addon definition", "Please fill out this build's version!", "Ok");
                    return;
                }

                List<BuildTarget> unsupportedBuildTargets = BuildTargets.Keys.Where(bt => !IsPlatformSupported(bt)).ToList();
                if (unsupportedBuildTargets.Count > 0) {
                    string unsupportedPlatforms = string.Join(", ", unsupportedBuildTargets.Select(bt => BuildTargets[bt]));
                    if (!EditorUtility.DisplayDialog("Unsupported build targets", $"The following build targets are not supported on this machine:\n{unsupportedPlatforms}\n\nPlayers on these platform(s) will not be able to use your addon.\nWould you like to continue anyway?", "Continue", "Cancel")) {
                        return;
                    }
                }

                string savePath = $"ExportedAddons/{selectedAddon.FolderName}-{selectedAddon.BuildDefinition.ReleaseVersion}";

                if (Directory.Exists(savePath)) {
                    if (!EditorUtility.DisplayDialog("Addon exists", $"The addon build path {savePath} already exists.\nCreating an addon with the same name + version as another might be confusing.\n\nWould you like to overwrite the existing files and continue the build anyway?", "Continue", "Cancel")) {
                        return;
                    }
                    Directory.Delete(savePath, true);
                }
                Directory.CreateDirectory(savePath);

                // Clean old addon folder
                string buildPath = "ExportedAddons/~~temp";
                try {
                    Directory.Delete(buildPath, true);
                } catch { }
                Directory.CreateDirectory(buildPath);

                // Export the build definition to the export folder
                File.WriteAllText($"{buildPath}/addon.json", JsonConvert.SerializeObject(selectedAddon.BuildDefinition));

                // Add icon
                if (selectedAddon.EditorDefinition.IconTexture) {
                    RenderTexture outputTexture = RenderTexture.GetTemporary(selectedAddon.EditorDefinition.IconTexture.width, selectedAddon.EditorDefinition.IconTexture.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Default);
                    Graphics.Blit(selectedAddon.EditorDefinition.IconTexture, outputTexture);
                    Texture2D writeableIcon = outputTexture.ToTexture2D();
                    File.WriteAllBytes(buildPath + "/icon.png", writeableIcon.EncodeToPNG());
                    RenderTexture.ReleaseTemporary(outputTexture);
                    DestroyImmediate(writeableIcon);
                }

                // Generate list of asset bundles.
                List<AssetBundleBuild> buildMap = new() {
                    new() {
                        assetBundleName = "basegame-assets",
                        assetNames = AssetDatabase.GetAssetPathsFromAssetBundle("basegame-assets"),
                    },
                    new() {
                        assetBundleName = "basegame-scenes",
                        assetNames = AssetDatabase.GetAssetPathsFromAssetBundle("basegame-scenes"),
                    }
                };

                string[] sceneAssets =
                    AssetDatabase.FindAssets("t:Scene", new[] { selectedAddon.FolderPath })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .ToArray();
                if (sceneAssets.Length > 0) {
                    buildMap.Add(new() {
                        assetBundleName = selectedAddon.BuildDefinition.ReleaseGuid + "-scenes",
                        assetNames = sceneAssets,
                    });
                }

                string[] nonSceneAssets =
                    AssetDatabase.FindAssets("", new[] { selectedAddon.FolderPath })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Except(sceneAssets)
                    .ToArray();
                if (nonSceneAssets.Length > 0) {
                    buildMap.Add(new() {
                        assetBundleName = selectedAddon.BuildDefinition.ReleaseGuid + "-assets",
                        assetNames = nonSceneAssets,
                    });
                }

                // Build asset bundles
                int steps = BuildTargets.Count + 1;
                int counter = 0;

                var oldBuildTarget = EditorUserBuildSettings.activeBuildTarget;
                var oldBuildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
                var buildMapArray = buildMap.ToArray();
                List<BuildTarget> failedBuilds = new();
                foreach (var buildTarget in BuildTargets.Keys.Except(unsupportedBuildTargets).ToList()) {
                    try {
                        EditorUtility.DisplayProgressBar("Building addon", $"Building for {buildTarget} build target...", (float) ++counter / steps);
                        string platformBuildPath = buildPath + "/" + buildTarget;
                        Directory.CreateDirectory(platformBuildPath);
                        BuildPipeline.BuildAssetBundles(platformBuildPath, 
                            buildMapArray, 
                            BuildAssetBundleOptions.AppendHashToAssetBundleName | BuildAssetBundleOptions.AssetBundleStripUnityVersion, 
                            buildTarget);

                        // Delete {buildTarget} bundle- it only contains a manifest and causes name collisions.
                        File.Delete($"{platformBuildPath}/{buildTarget}");

                        // Delete .manifests
                        foreach (var manifest in Directory.EnumerateFiles(platformBuildPath, "*.manifest", new EnumerationOptions { RecurseSubdirectories = true })) {
                            File.Delete(manifest);
                        }

                        // Delete base game assets / scenes
                        foreach (var basegameBundle in Directory.EnumerateFiles(platformBuildPath, "*", new EnumerationOptions { RecurseSubdirectories = true }).Where(filename => filename.Contains("basegame"))) {
                            File.Delete(basegameBundle);
                        }

                        Debug.Log($"Successfully built addon for platform {buildTarget}");
                    } catch (Exception e) {
                        Debug.LogError($"Failed to export addon for platform {buildTarget}");
                        Debug.LogError(e);
                        failedBuilds.Add(buildTarget);
                    }
                }

                EditorUserBuildSettings.SwitchActiveBuildTarget(oldBuildTargetGroup, oldBuildTarget);

                // Create standalone addons
                foreach ((var buildTarget, var buildTargetPretty) in BuildTargets) {
                    if (failedBuilds.Contains(buildTarget)) {
                        continue;
                    }

                    string platformZipPath = $"{savePath}/{selectedAddon.FolderName}-{selectedAddon.BuildDefinition.ReleaseVersion}-{buildTargetPretty}{AddonManager.AddonExtension}";
                    ZipFile.CreateFromDirectory(buildPath, platformZipPath);

                    // Open and remove unrelated entries
                    using var platformZip = ZipFile.Open(platformZipPath, ZipArchiveMode.Update);
                    var entriesToRemove = platformZip.Entries
                        .Where(en => en.FullName.Contains('/') && !en.FullName.StartsWith($"{buildTarget}/"))
                        .ToList();

                    foreach (var entry in entriesToRemove) {
                        entry.Delete();
                    }

                    Debug.Log($"{buildTargetPretty} build saved to {platformZipPath}");
                }

                // Create universal addon
                string universalZipPath = $"{savePath}/{selectedAddon.FolderName}-{selectedAddon.BuildDefinition.ReleaseVersion}-Universal{AddonManager.AddonExtension}";
                EditorUtility.DisplayProgressBar("Building addon", "Compressing into .mvladdon...", (float) ++counter / steps);
                ZipFile.CreateFromDirectory(buildPath, universalZipPath);
                Debug.Log($"Universal build saved to {universalZipPath}");

                // Clean
                try {
                    Directory.Delete(buildPath, true);
                } catch { }
                EditorUtility.ClearProgressBar();

                if (failedBuilds.Count > 0) {
                    Debug.LogError($"Addon build error: The following builds failed:\n* {string.Join("\n* ", failedBuilds)}");
                    EditorUtility.DisplayDialog("Build(s) Failed", $"The following builds failed:\n* {string.Join("\n* ", failedBuilds)}", "OK");
                } else {
                    EditorUtility.DisplayDialog("Build Successful", $"The addon was saved to {savePath}", "OK");
                }

                Close();
            }
        }

        // https://discussions.unity.com/t/detect-if-build-target-is-installed-183801/183801
        private static bool IsPlatformSupported(BuildTarget buildTarget) {
            var moduleManager = System.Type.GetType("UnityEditor.Modules.ModuleManager,UnityEditor.dll");
            var isPlatformSupportLoaded = moduleManager.GetMethod("IsPlatformSupportLoaded", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var getTargetStringFromBuildTarget = moduleManager.GetMethod("GetTargetStringFromBuildTarget", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

            return (bool) isPlatformSupportLoaded.Invoke(null, new object[] { (string) getTargetStringFromBuildTarget.Invoke(null, new object[] { buildTarget }) });
        }

        public class AddonCreateWindow : EditorWindow {

            private string id = "addon" + Guid.NewGuid().ToString()[..8].ToUpper(), displayName = "New Addon", author = "Unknown", description = "A brand-new MvLO addon.";

            [MenuItem("Tools/MvLO/Addons/Create", secondaryPriority = 1)]
            public static void CreateAddon() {
                GetWindow<AddonCreateWindow>();
            }

            public void OnGUI() {
                EditorGUILayout.LabelField("Addon information", EditorStyles.boldLabel);

                id = EditorGUILayout.TextField("Addon Unique ID", id);
                displayName = EditorGUILayout.TextField("Name", displayName);
                author = EditorGUILayout.TextField("Author", author);
                description = EditorGUILayout.TextField("Description", description);

                if (GUILayout.Button("Create")) {
                    string folderName = string.Concat(displayName.Split(Path.GetInvalidFileNameChars()));

                    // Create folder
                    string folder = $"Assets/Addons/{folderName}";
                    if (Directory.Exists(folder)) {
                        if (!EditorUtility.DisplayDialog("Addon already exists!", $"An addon already exists with this name.\nDo you want to overwrite it's addon definition?\n(Assets will be preserved.)", "Yes", "Cancel")) {
                            return;
                        }
                    } else {
                        Directory.CreateDirectory(folder);
                    }

                    // Write addon definition
                    var definition = ScriptableObject.CreateInstance<AddonDefinition>();
                    definition.AddonId = id;
                    definition.DisplayName = displayName;
                    definition.Author = author;
                    definition.Description = description;
                    AssetDatabase.CreateAsset(definition, $"{folder}/{displayName.Replace(" ", "")}AddonDefinition.asset");

                    EditorUtility.DisplayDialog("Addon created!", $"Successfully created a new addon \"{displayName}\".\n\nFolder: {folder}\nAny assets/stages placed inside this folder will be included in the addon when built.", "Ok");
                    Close();
                }
            }
        }
    }
}