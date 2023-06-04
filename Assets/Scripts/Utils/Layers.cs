using UnityEngine;

namespace NSMB.Utils {
    public static class Layers {

        private static int? _maskAnyGround, _maskSolidGround, _maskOnlyPlayer;
        public static int MaskAnyGround     => LazyLoadMask(ref _maskAnyGround, LayerGround, LayerSemisolid, LayerGroundEntity);
        public static int MaskSolidGround   => LazyLoadMask(ref _maskSolidGround, LayerGround, LayerGroundEntity);
        public static int MaskOnlyPlayers   => LazyLoadMask(ref _maskOnlyPlayer, LayerPlayer);

        private static int? _layerGround, _layerSemisolid, _layerHitsNothing, _layerDefault, _layerPassthrough, _layerLooseCoin, _layerEntity, _layerEntityHitbox, _layerPlayer, _layerGroundEntity;
        public static int LayerGround       => LazyLoadLayer(ref _layerGround, "Ground");
        public static int LayerSemisolid    => LazyLoadLayer(ref _layerSemisolid, "Semisolids");
        public static int LayerHitsNothing  => LazyLoadLayer(ref _layerHitsNothing, "HitsNothing");
        public static int LayerDefault      => LazyLoadLayer(ref _layerDefault, "Default");
        public static int LayerPassthrough  => LazyLoadLayer(ref _layerPassthrough, "PassthroughInvalid");
        public static int LayerLooseCoin    => LazyLoadLayer(ref _layerLooseCoin, "LooseCoin");
        public static int LayerEntity       => LazyLoadLayer(ref _layerEntity, "Entity");
        public static int LayerEntityHitbox => LazyLoadLayer(ref _layerEntityHitbox, "EntityHitbox");
        public static int LayerPlayer       => LazyLoadLayer(ref _layerPlayer, "Player");
        public static int LayerGroundEntity => LazyLoadLayer(ref _layerGroundEntity, "GroundEntity");

        private static int LazyLoadMask(ref int? variable, params int[] layers) {
            if (variable != null)
                return (int) variable;

            variable = 0;
            foreach (int layer in layers)
                variable |= 1 << layer;

            return (int) variable;
        }

        private static int LazyLoadLayer(ref int? variable, string layer) {
            if (variable != null)
                return (int) variable;

            return (int) (variable = LayerMask.NameToLayer(layer));
        }
    }
}
