namespace Quantum {
    public unsafe class PowerupTile : PowerupTileBase {

        public AssetRef<CoinItemAsset> smallPowerup, largePowerup;
        public bool bypassCustomSpawnWeightDisables;

        public override CoinItemAsset GetItemAsset(Frame f, EntityRef marioEntity, MarioPlayer* mario) {
            bool isSmall = mario->CurrentPowerupState < PowerupState.Mushroom;
            var powerupToSpawn = isSmall ? smallPowerup : largePowerup;

            if (!bypassCustomSpawnWeightDisables) {
                ref var rules = ref f.Global->Rules;

                if (rules.IsCoinItemDisabled(f, powerupToSpawn)) {
                    // Try the other one.
                    powerupToSpawn = isSmall ? largePowerup : smallPowerup;

                    if (rules.IsCoinItemDisabled(f, powerupToSpawn)) {
                        // Both are disabled... give up.
                        return null;
                    }
                }
            }

            return f.FindAsset(powerupToSpawn);
        }
    }
}
