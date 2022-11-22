using UnityEngine.SceneManagement;

namespace Fusion {
  public static class NetworkRunnerExtensions {
    public static bool SetActiveScene(this NetworkRunner runner, string sceneNameOrPath) {
      if (TryGetSceneBuildIndex(sceneNameOrPath, out var buildIndex)) {
        runner.SetActiveScene(buildIndex);
        return true;
      } else {
        return false;
      }
    }

    static bool TryGetSceneBuildIndex(string nameOrPath, out int buildIndex) {
      if (nameOrPath.IndexOf('/') >= 0) {
        buildIndex = SceneUtility.GetBuildIndexByScenePath(nameOrPath);
        if (buildIndex < 0) {
          buildIndex = -1;
          return false;
        } else {
          return true;
        }
      } else {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; ++i) {
          var scenePath = SceneUtility.GetScenePathByBuildIndex(i);
          GetFileNameWithoutExtensionPosition(scenePath, out var nameIndex, out var nameLength);
          if (nameLength == nameOrPath.Length && string.Compare(scenePath, nameIndex, nameOrPath, 0, nameLength, true) == 0) {
            buildIndex = i;
            return true;
          }
        }

        buildIndex = -1;
        return false;
      }
    }

    static void GetFileNameWithoutExtensionPosition(string nameOrPath, out int index, out int length) {
      var lastSlash = nameOrPath.LastIndexOf('/');
      if (lastSlash >= 0) {
        index = lastSlash + 1;
      } else {
        index = 0;
      }

      var lastDot = nameOrPath.LastIndexOf('.');
      if (lastDot > index) {
        length = lastDot - index;
      } else {
        length = nameOrPath.Length - index;
      }
    }
  }
}
