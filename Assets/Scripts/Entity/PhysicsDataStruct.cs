using UnityEngine;

using Fusion;
using NSMB.Utils;

[System.Serializable]
public struct PhysicsDataStruct : INetworkStruct {

    public byte Flags;
    public float FloorAngle;

    [Networked, Capacity(8)] public NetworkLinkedList<Vector2Int> TilesStandingOn => default;
    [Networked, Capacity(8)] public NetworkLinkedList<Vector2Int> TilesHitSide => default;
    [Networked, Capacity(8)] public NetworkLinkedList<Vector2Int> TilesHitRoof => default;

    public bool OnGround {
        get => Utils.BitTest(Flags, 0);
        set => Utils.BitSet(ref Flags, 0, value);
    }
    public bool CrushableGround {
        get => Utils.BitTest(Flags, 1);
        set => Utils.BitSet(ref Flags, 1, value);
    }
    public bool HitRoof {
        get => Utils.BitTest(Flags, 2);
        set => Utils.BitSet(ref Flags, 2, value);
    }
    public bool HitLeft {
        get => Utils.BitTest(Flags, 3);
        set => Utils.BitSet(ref Flags, 3, value);
    }
    public bool HitRight {
        get => Utils.BitTest(Flags, 4);
        set => Utils.BitSet(ref Flags, 4, value);
    }

    public override string ToString() {
        return $"FloorAngle: {FloorAngle} OnGround: {OnGround}, CrushableGround: {CrushableGround}, HitRoof: {HitRoof}, HitLeft: {HitLeft}, HitRight: {HitRight}";
    }
}
