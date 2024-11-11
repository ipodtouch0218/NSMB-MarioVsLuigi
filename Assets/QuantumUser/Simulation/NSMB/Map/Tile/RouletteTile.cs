using Quantum;

public unsafe class RouletteTile : PowerupTileBase {
    public override unsafe PowerupAsset GetPowerupAsset(Frame f, EntityRef marioEntity, MarioPlayer* mario) {
        return QuantumUtils.GetRandomItem(f, mario);
    }
}