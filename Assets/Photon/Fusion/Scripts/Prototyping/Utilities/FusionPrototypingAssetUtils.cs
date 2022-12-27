#if UNITY_EDITOR

using UnityEditor;

namespace FusionPrototypingInternal {
  internal static class FusionPrototypingAssetUtils {
    public static T GetAsset<T>(this string Guid, ref T backing) where T : UnityEngine.Object {
      if (backing != null)
        return backing;

      var path = AssetDatabase.GUIDToAssetPath(Guid);
      if (path != null && path != "")
        backing = AssetDatabase.LoadAssetAtPath<T>(path);
      return GetAsset<T>(Guid);
    }

    public static T GetAsset<T>(this string Guid) where T : UnityEngine.Object {
      var path = AssetDatabase.GUIDToAssetPath(Guid);
      if (string.IsNullOrEmpty( path)) {
        return null;
      } else {
        return AssetDatabase.LoadAssetAtPath<T>(path);
      }
    }
  }
}

#endif
