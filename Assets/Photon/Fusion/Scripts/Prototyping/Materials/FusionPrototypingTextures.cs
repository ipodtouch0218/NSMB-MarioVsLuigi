#if UNITY_EDITOR

using UnityEngine;
using FusionPrototypingInternal;

public static class FusionPrototypingTextures
{
  // Box Grid Texture
  private const string _boxGridGuid = "933188806222c8f47969cdea92996cc9";
  private static Texture2D _boxGrid;
  public static Texture2D BoxGrid { get { return _boxGridGuid.GetAsset(ref _boxGrid); } }

}

#endif
