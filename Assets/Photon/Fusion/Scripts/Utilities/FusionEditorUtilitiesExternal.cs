#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fusion.Editor {

  // Editor scripts that are kept outside of the Editor folder, so they are accessible to MonoBehaviours inside of UNITY_EDITOR sections.
  public static class FusionEditorUtilitiesExternal {

    public static void AddSceneToBuildSettings(this Scene scene) {
      var buildScenes = EditorBuildSettings.scenes;
      bool isInBuildScenes = false;
      foreach (var bs in buildScenes) {
        if (bs.path == scene.path) {
          isInBuildScenes = true;
          break;
        }
      }
      if (isInBuildScenes == false) {
        var buildList = new List<EditorBuildSettingsScene>();
        buildList.Add(new EditorBuildSettingsScene(scene.path, true));
        buildList.AddRange(buildScenes);
        Debug.Log($"Added '{scene.path}' as first entry in Build Settings.");
        EditorBuildSettings.scenes = buildList.ToArray();
      }
    }

    public static bool TryGetSceneIndexInBuildSettings(this Scene scene, out int index) {
      var buildScenes = EditorBuildSettings.scenes;
      for (int i = 0; i < buildScenes.Length; ++i) {
        var bs = buildScenes[i];
        if (bs.path == scene.path) {
          index = i;
          return true;
        }
      }
      index = -1;
      return false;
    }
  }
}

#endif
