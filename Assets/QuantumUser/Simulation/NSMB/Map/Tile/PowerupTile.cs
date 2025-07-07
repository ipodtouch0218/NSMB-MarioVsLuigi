using Quantum;

public unsafe class PowerupTile : PowerupTileBase {

    public AssetRef<PowerupAsset> smallPowerup, largePowerup;

    public override CoinItemAsset GetItemAsset(Frame f, EntityRef marioEntity, MarioPlayer* mario) {
        return f.FindAsset(mario->CurrentPowerupState < PowerupState.Mushroom ? smallPowerup : largePowerup);
    }
}
