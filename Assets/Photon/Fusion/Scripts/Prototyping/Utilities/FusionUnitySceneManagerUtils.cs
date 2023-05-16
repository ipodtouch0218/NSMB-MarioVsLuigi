using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace Fusion {
  
  public static class FusionUnitySceneManagerUtils {
    public static int GetSceneBuildIndex(string nameOrPath) {
      if (nameOrPath.IndexOf('/') >= 0) {
        return SceneUtility.GetBuildIndexByScenePath(nameOrPath);
      } else {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; ++i) {
          var scenePath = SceneUtility.GetScenePathByBuildIndex(i);
          GetFileNameWithoutExtensionPosition(scenePath, out var nameIndex, out var nameLength);
          if (nameLength == nameOrPath.Length && string.Compare(scenePath, nameIndex, nameOrPath, 0, nameLength, true) == 0) {
            return i;
          }
        }

        return -1;
      }
    }
    
    public static int GetSceneIndex(IList<string> scenePathsOrNames, string nameOrPath) {
      if (nameOrPath.IndexOf('/') >= 0) {
        return scenePathsOrNames.IndexOf(nameOrPath);
      } else {
        for (int i = 0; i < scenePathsOrNames.Count; ++i) {
          var scenePath = scenePathsOrNames[i];
          GetFileNameWithoutExtensionPosition(scenePath, out var nameIndex, out var nameLength);
          if (nameLength == nameOrPath.Length && string.Compare(scenePath, nameIndex, nameOrPath, 0, nameLength, true) == 0) {
            return i;
          }
        }
        return -1;
      }
    }

    public static void GetFileNameWithoutExtensionPosition(string nameOrPath, out int index, out int length) {
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