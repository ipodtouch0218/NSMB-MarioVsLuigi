using Photon.Deterministic;
using Quantum;

public class CoinItemAsset : AssetObject {

    public AssetRef<EntityPrototype> Prefab;
    public FP SpawnChance = FP._0_10, LosingSpawnBonus = 0;
    public SoundEffect BlockSpawnSoundEffect = SoundEffect.World_Block_Powerup;
    public bool BigPowerup, VerticalPowerup, CustomPowerup, LivesOnlyPowerup;
    public bool CanSpawnFromBlock = true;
    public bool OnlyOneCanExist = false;

}