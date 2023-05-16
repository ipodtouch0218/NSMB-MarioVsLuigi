using UnityEngine.SceneManagement;

namespace Fusion {
  public static class NetworkRunnerExtensions {
    public static bool SetActiveScene(this NetworkRunner runner, string sceneNameOrPath) {
      if (runner.SceneManager is NetworkSceneManagerBase networkSceneManager) {
        if (networkSceneManager.TryGetSceneRef(sceneNameOrPath, out var sceneRef)) {
          runner.SetActiveScene(sceneRef);
          return true;
        }
        return false;
      } else {
        // fallback to the build index
        var buildIndex = FusionUnitySceneManagerUtils.GetSceneBuildIndex(sceneNameOrPath);
        if (buildIndex >= 0) {
          runner.SetActiveScene(buildIndex);
          return true;
        }

        return false;
      }
    }
  }
}
