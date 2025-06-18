namespace Quantum {
    public partial class SimulationConfig : AssetObject {

        public AssetRef<StageTile> InvisibleSolidTile;

        public AssetRef<Map>[] AllStages;
        public AssetRef<CharacterAsset>[] CharacterDatas;
        public AssetRef<PaletteSet>[] Palettes;
        public AssetRef<TeamAsset>[] Teams;
        public AssetRef<EntityPrototype> FireballPrototype, IceballPrototype, HammerPrototype, BlockBumpPrototype, IceBlockPrototype;

        public GameRules DefaultRules;
    }
}