using Photon.Deterministic;
using Quantum;
using System;

public class GenericMoverAsset : AssetObject {
    public PathNode[] ObjectPath;
    public LoopingMode LoopMode;

    [Serializable]
    public struct PathNode {
        public FPVector2 Position;
        public FP TravelDuration;
        public bool EaseIn;
        public bool EaseOut;
    }

    public enum LoopingMode {
        Clamp,
        Loop,
        PingPong,
    }
}
