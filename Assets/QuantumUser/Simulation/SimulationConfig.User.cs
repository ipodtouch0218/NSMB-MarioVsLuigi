namespace Quantum {
    public partial class SimulationConfig : AssetObject {

        public StageTile InvisibleSolidTile;

        public PowerupAsset[] AllPowerups;
        public PowerupAsset FallbackPowerup;
        public CharacterAsset[] CharacterDatas;

        public AssetRef<EntityPrototype> FireballPrototype, IceballPrototype, BigStarPrototype, BlockBumpPrototype;

        public int StarsToWin;
        public int CoinsForPowerup;
        public int Lives;
        public bool LivesEnabled => Lives > 0;
        public int TimerSeconds;
        public bool TimerEnabled => TimerSeconds > 0;

        public bool TeamsEnabled;
        public bool CustomPowerupsEnabled;

    }
}