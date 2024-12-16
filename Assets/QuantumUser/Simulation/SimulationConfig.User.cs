namespace Quantum {
    public partial class SimulationConfig : AssetObject {

        public StageTile InvisibleSolidTile;

        public PowerupAsset[] AllPowerups;
        public PowerupAsset FallbackPowerup;
        public CharacterAsset[] CharacterDatas;
        public TeamAsset[] Teams;

        public EntityPrototype FireballPrototype, IceballPrototype, BigStarPrototype, BlockBumpPrototype, LooseCoinPrototype, IceBlockPrototype;

        public GameRules DefaultRules;
    }
}