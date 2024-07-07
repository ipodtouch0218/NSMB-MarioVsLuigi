#if !QUANTUM_DEV

#region Assets/Photon/QuantumMenu/Editor/QuantumMenuConfigEditor.cs

namespace Quantum.Menu.Editor {
  using UnityEditor;
  using UnityEngine.SceneManagement;
  using UnityEngine;
  using System.Linq;
  using System.Collections.Generic;
  using static QuantumUnityExtensions;

  /// <summary>
  /// Custom inspector for <see cref="QuantumMenuConfig"/>
  /// </summary>
  [CustomEditor(typeof(QuantumMenuConfig))]
  public class QuantumMenuConfigEditor : Editor {
    /// <summary>
    /// Overriding drawing.
    /// </summary>
    public override void OnInspectorGUI() {
      base.OnInspectorGUI();

      if (GUILayout.Button("AddCurrentSceneToAvailableScenes")) {
        AddCurrentScene((QuantumMenuConfig)target, null);
      }
    }

    /// <summary>
    /// Add the current open Unity scene to a QuantumMenuConfig.
    /// </summary>
    /// <param name="menuConfig">The menu config asset</param>
    /// <param name="runtimeConfig">Set an optional <see cref="RuntimeConfig"/></param>
    public static void AddCurrentScene(QuantumMenuConfig menuConfig, RuntimeConfig runtimeConfig) {
      var mapData = FindFirstObjectByType<QuantumMapData>();
      if (mapData == null) {
        QuantumEditorLog.Error($"Map asset not found in current scene");
        return;
      }

      var debugRunner = FindAnyObjectByType<QuantumRunnerLocalDebug>();

      var scene = SceneManager.GetActiveScene();

      var scenePath = PathUtils.MakeSane(scene.path);
      if (menuConfig.AvailableScenes.Any(s => scenePath.Equals(s.ScenePath, System.StringComparison.Ordinal))) {
        return;
      }

      var sceneInfo = new PhotonMenuSceneInfo {
        Name = scene.name,
        Preview = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath("e43d4c530865a8342957f23fe8a873b2")),
        ScenePath = scenePath,
        RuntimeConfig = runtimeConfig ?? debugRunner?.RuntimeConfig ?? new RuntimeConfig()
      };

      if (sceneInfo.Map.IsValid == false) {
        sceneInfo.RuntimeConfig.Map = mapData.Asset;
      }

      menuConfig.AvailableScenes.Add(sceneInfo);

      AddScenePathToBuildSettings(scenePath);
    }

    private static void AddScenePathToBuildSettings(string scenePath) {
      var editorBuildSettingsScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
      if (editorBuildSettingsScenes.FindIndex(s => s.path.Equals(scenePath, System.StringComparison.Ordinal)) < 0) {
        editorBuildSettingsScenes.Add(new EditorBuildSettingsScene { path = scenePath, enabled = true });
        EditorBuildSettings.scenes = editorBuildSettingsScenes.ToArray();
      }
    }
  }
}

#endregion

#endif
