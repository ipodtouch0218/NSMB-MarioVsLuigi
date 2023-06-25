using UnityEngine;

namespace NSMB.Utils {
    public static class Layers {

        private static LayerMask? _maskAnyGround, _maskSolidGround, _maskOnlyPlayer;
        public static LayerMask MaskAnyGround       => LazyLoadMask(ref _maskAnyGround, LayerGround, LayerSemisolid, LayerGroundEntity);
        public static LayerMask MaskSolidGround     => LazyLoadMask(ref _maskSolidGround, LayerGround, LayerGroundEntity);
        public static LayerMask MaskOnlyPlayers     => LazyLoadMask(ref _maskOnlyPlayer, LayerPlayer);

        private static int? _layerGround, _layerSemisolid, _layerHitsNothing, _layerDefault, _layerPassthrough, _layerLooseCoin, _layerEntity, _layerEntityHitbox, _layerEntityNoGroundEntity, _layerPlayer, _layerGroundEntity;
        public static int LayerGround               => LazyLoadLayer(ref _layerGround, "Ground");
        public static int LayerSemisolid            => LazyLoadLayer(ref _layerSemisolid, "Semisolids");
        public static int LayerHitsNothing          => LazyLoadLayer(ref _layerHitsNothing, "HitsNothing");
        public static int LayerDefault              => LazyLoadLayer(ref _layerDefault, "Default");
        public static int LayerPassthrough          => LazyLoadLayer(ref _layerPassthrough, "PassthroughInvalid");
        public static int LayerLooseCoin            => LazyLoadLayer(ref _layerLooseCoin, "LooseCoin");
        public static int LayerEntity               => LazyLoadLayer(ref _layerEntity, "Entity");
        public static int LayerEntityNoGroundEntity => LazyLoadLayer(ref _layerEntityNoGroundEntity, "EntityNoGroundEntity");
        public static int LayerEntityHitbox         => LazyLoadLayer(ref _layerEntityHitbox, "EntityHitbox");
        public static int LayerPlayer               => LazyLoadLayer(ref _layerPlayer, "Player");
        public static int LayerGroundEntity         => LazyLoadLayer(ref _layerGroundEntity, "GroundEntity");

        private static LayerMask LazyLoadMask(ref LayerMask? variable, params int[] layers) {
            if (variable != null)
                return (LayerMask) variable;

            variable = 0;
            foreach (int layer in layers)
                variable |= (1 << layer);

            return (LayerMask) variable;
        }

        private static int LazyLoadLayer(ref int? variable, string layer) {
            if (variable != null)
                return (int) variable;

            return (int) (variable = LayerMask.NameToLayer(layer));
        }
    }
}
