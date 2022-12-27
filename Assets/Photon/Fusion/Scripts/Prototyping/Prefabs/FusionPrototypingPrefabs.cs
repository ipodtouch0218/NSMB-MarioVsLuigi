#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

public static class FusionPrototypingPrefabs
{

  private const string _basicPlayerGuid = "e4df49d0bf125a740a2c14ab6e887572";
  private static GameObject _basicPlayer;
  public static GameObject BasicPlayer { get { return GetPrefab(_basicPlayerGuid, ref _basicPlayer); } }

  private const string _basicPlayerRB2DGuid = "dbc9b57ea26fbf84b8cc14b0882fe89b";
  private static GameObject _basicPlayerRB2D;
  public static GameObject BasicPlayerRB2D { get { return GetPrefab(_basicPlayerRB2DGuid, ref _basicPlayerRB2D); } }

  private const string _ground2DGuid = "5998d41545749df4f82cfabc16cdd7a0";
  private static GameObject _ground2D;
  public static GameObject Ground2D { get { return GetPrefab(_ground2DGuid, ref _ground2D); } }

  private static GameObject GetPrefab(string guid, ref GameObject backingField) {
    if (backingField)
      return backingField;

    var path = AssetDatabase.GUIDToAssetPath(guid);
    backingField = AssetDatabase.LoadAssetAtPath<GameObject>(path);
    return backingField;
  }

}

#endif