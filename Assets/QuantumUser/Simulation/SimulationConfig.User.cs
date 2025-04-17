namespace Quantum {
    public partial class SimulationConfig : AssetObject {

        public AssetRef<StageTile> InvisibleSolidTile;

        public AssetRef<PowerupAsset>[] AllPowerups;
        public AssetRef<Map>[] AllStages;
        public AssetRef<PowerupAsset >FallbackPowerup;
        public AssetRef<CharacterAsset>[] CharacterDatas;
        public AssetRef<TeamAsset>[] Teams;

        public AssetRef<EntityPrototype> FireballPrototype, IceballPrototype, HammerPrototype, BigStarPrototype, BlockBumpPrototype, LooseCoinPrototype, IceBlockPrototype;

        public GameRules DefaultRules;
    }
}