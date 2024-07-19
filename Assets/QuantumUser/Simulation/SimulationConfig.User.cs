namespace Quantum {
    public partial class SimulationConfig : AssetObject {

        public PowerupAsset[] AllPowerups;
        public PowerupAsset FallbackPowerup;

        public AssetRef<EntityPrototype> FireballPrototype, IceballPrototype, BigStarPrototype;

        public int StarsToWin;
        public int CoinsForPowerup;
        public int Lives;
        public bool LivesEnabled => Lives > 0;

        public bool TeamsEnabled;
        public bool CustomPowerupsEnabled;

    }
}