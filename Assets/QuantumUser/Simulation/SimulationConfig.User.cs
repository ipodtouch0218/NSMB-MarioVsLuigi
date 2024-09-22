namespace Quantum {
    public partial class SimulationConfig : AssetObject {

        public Map LobbyMap;

        public StageTile InvisibleSolidTile;

        public PowerupAsset[] AllPowerups;
        public PowerupAsset FallbackPowerup;
        public CharacterAsset[] CharacterDatas;

        public AssetRef<EntityPrototype> FireballPrototype, IceballPrototype, BigStarPrototype, BlockBumpPrototype, LooseCoinPrototype, IceBlockPrototype;

        public GameRules DefaultRules;
    }
}