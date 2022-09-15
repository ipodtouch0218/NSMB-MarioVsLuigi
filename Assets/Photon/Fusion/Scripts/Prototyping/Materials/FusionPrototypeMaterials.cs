#if UNITY_EDITOR

using UnityEngine;
using FusionPrototypingInternal;

public static class FusionPrototypeMaterials
{

  // Default Material
  private const string _defaultGuid = "38fbbd3db9e06c54e8a11016e2381469";
  private static Material _default;
  public static Material Default { get { return _defaultGuid.GetAsset(ref _default); } }

  // Black Material
  private const string _blackGuid = "5d1b896bc311a1d438c929c45b0c5fbc";
  private static Material _black;
  public static Material Black { get { return _blackGuid.GetAsset(ref _black); } }

  // red Material
  private const string _redGuid = "42b24d338df372049a18bd257e6bc550";
  private static Material _red;
  public static Material Red { get { return _redGuid.GetAsset(ref _red); } }

  // green Material
  private const string _greenGuid = "8f9372f58500a3f46ba541ea24e6d105";
  private static Material _green;
  public static Material Green { get { return _greenGuid.GetAsset(ref _green); } }

  // Blue Material
  private const string _blueGuid = "1e79a222d16731a4e96e7360bf14bdfd";
  private static Material _blue;
  public static Material Blue { get { return _blueGuid.GetAsset(ref _blue); } }

  // Yellow Material
  private const string _yellowGuid = "37cb86b5e2e83fb47a760ff23a51aff6";
  private static Material _yellow;
  public static Material Yellow { get { return _yellowGuid.GetAsset(ref _yellow); } }

  // Yellow Material
  private const string _cyanGuid = "1ed66b76f883f11419ff9b35e5f31616";
  private static Material _cyan;
  public static Material Cyan { get { return _cyanGuid.GetAsset(ref _cyan); } }

  // Floor Material
  private const string _floorGuid = "002db72054162e04b917e86a395e0a0f";
  private static Material _floor;
  public static Material Floor { get { return _floorGuid.GetAsset(ref _floor); } }

  // Box Material
  private const string _boxGuid = "2544ff8e0cb0b4649ad11a93d3259ffa";
  private static Material _box;
  public static Material Box { get { return _boxGuid.GetAsset(ref _box); } }


  //private static Material GetMaterial(string Guid, ref Material backing) {
  //  if (backing != null)
  //    return backing;

  //  var path = AssetDatabase.GUIDToAssetPath(Guid);
  //  if (path != null && path != "")
  //    backing = AssetDatabase.LoadAssetAtPath<Material>(path);
  //  return backing;
  //}

}

#endif