namespace Quantum {
  using System;
  using Photon.Deterministic;
  using UnityEngine;

  /// <summary>
  /// The script will create a static 3D terrain collider during Quantum map baking.
  /// </summary>
  [ExecuteInEditMode]
  public class QuantumStaticTerrainCollider3D : QuantumMonoBehaviour {
    /// <summary>
    /// The Quantum terrain collider asset.
    /// </summary>
    [InlineHelp]
    public TerrainCollider Asset;
    /// <summary>
    /// Additional static collider settings.
    /// </summary>
    [InlineHelp, DrawInline, Space]
    public QuantumStaticColliderSettings Settings = new QuantumStaticColliderSettings();

    /// <summary>
    /// The physics solver will resolve sphere and capsule shapes against terrain collisions as if it was a regular flat and smooth plane.
    /// </summary>
    [HideInInspector]
    public Boolean SmoothSphereMeshCollisions = false;

#pragma warning disable 618 // use of obsolete
    [Obsolete("Use 'Settings.MutableMode' instead.")]
    public PhysicsCommon.StaticColliderMutableMode MutableMode => Settings.MutableMode;
#pragma warning restore 618

    public void Bake() {
#if QUANTUM_ENABLE_TERRAIN && !QUANTUM_DISABLE_TERRAIN
      FPMathUtils.LoadLookupTables();

      var t = GetComponent<Terrain>();

      Asset.Resolution = t.terrainData.heightmapResolution;

      Asset.HeightMap = new FP[Asset.Resolution * Asset.Resolution];
      Asset.Position = transform.position.ToFPVector3();
      Asset.Scale = t.terrainData.heightmapScale.ToFPVector3();

      for (int i = 0; i < Asset.Resolution; i++) {
        for (int j = 0; j < Asset.Resolution; j++) {
          Asset.HeightMap[j + i * Asset.Resolution] = FP.FromFloat_UNSAFE(t.terrainData.GetHeight(i, j));
        }
      }

      // support to Terrain Paint Holes: https://docs.unity3d.com/2019.4/Documentation/Manual/terrain-PaintHoles.html
      Asset.HoleMask = new ulong[(Asset.Resolution * Asset.Resolution - 1) / 64 + 1];

      for (int i = 0; i < Asset.Resolution - 1; i++) {
        for (int j = 0; j < Asset.Resolution - 1; j++) {
          if (t.terrainData.IsHole(i, j)) {
            Asset.SetHole(i, j);
          }
        }
      }

#if UNITY_EDITOR
      UnityEditor.EditorUtility.SetDirty(Asset);
      UnityEditor.EditorUtility.SetDirty(this);
#endif
#endif
    }
  }
}