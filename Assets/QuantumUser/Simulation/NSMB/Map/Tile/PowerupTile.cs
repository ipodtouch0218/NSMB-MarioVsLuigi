using Quantum;

public unsafe class PowerupTile : PowerupTileBase {

    public PowerupAsset smallPowerup, largePowerup;

    public override PowerupAsset GetPowerupAsset(Frame f, EntityRef marioEntity, MarioPlayer* mario) {
        return mario->CurrentPowerupState < PowerupState.Mushroom ? smallPowerup : largePowerup;
    }
}
