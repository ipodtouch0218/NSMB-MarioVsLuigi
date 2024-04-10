using System.Collections.Generic;
using UnityEngine;

namespace NSMB.Utils {
    public static class Layers {

        // Layer Masks
        private static LayerMask? _maskAnyGround, _maskSolidGround, _maskOnlyPlayer, _maskEntities;
        public static LayerMask MaskAnyGround       => LazyLoadMask(ref _maskAnyGround, LayerGround, LayerSemisolid, LayerGroundEntity);
        public static LayerMask MaskSolidGround     => LazyLoadMask(ref _maskSolidGround, LayerGround, LayerGroundEntity);
        public static LayerMask MaskOnlyPlayers     => LazyLoadMask(ref _maskOnlyPlayer, LayerPlayer);
        public static LayerMask MaskEntities        => LazyLoadMask(ref _maskEntities, LayerPlayer, LayerGroundEntity, LayerEntity, LayerEntityHitbox, LayerEntityNoGroundEntity);

        // Collision Matrix Masks
        private static readonly Dictionary<int, LayerMask> CollisionMatrixMasks = new();

        // Layer Ints
        private static int? _layerGround, _layerSemisolid, _layerHitsNothing, _layerPassthrough, _layerEntity, _layerEntityHitbox, _layerEntityNoGroundEntity, _layerPlayer, _layerGroundEntity;
        public static int LayerGround               => LazyLoadLayer(ref _layerGround, "Ground");
        public static int LayerSemisolid            => LazyLoadLayer(ref _layerSemisolid, "Semisolids");
        public static int LayerHitsNothing          => LazyLoadLayer(ref _layerHitsNothing, "HitsNothing");
        public static int LayerPassthrough          => LazyLoadLayer(ref _layerPassthrough, "PassthroughInvalid");
        public static int LayerEntity               => LazyLoadLayer(ref _layerEntity, "Entity");
        public static int LayerEntityNoGroundEntity => LazyLoadLayer(ref _layerEntityNoGroundEntity, "EntityNoGroundEntity");
        public static int LayerEntityHitbox         => LazyLoadLayer(ref _layerEntityHitbox, "EntityHitbox");
        public static int LayerPlayer               => LazyLoadLayer(ref _layerPlayer, "Player");
        public static int LayerGroundEntity         => LazyLoadLayer(ref _layerGroundEntity, "GroundEntity");

        public static LayerMask GetCollisionMask(int layer) {
            if (CollisionMatrixMasks.TryGetValue(layer, out LayerMask collisionMask)) {
                return collisionMask;
            }

            int mask = 0;
            for (int j = 0; j < 32; j++) {
                if (!Physics2D.GetIgnoreLayerCollision(layer, j)) {
                    mask |= 1 << j;
                }
            }
            return CollisionMatrixMasks[layer] = mask;
        }

        private static LayerMask LazyLoadMask(ref LayerMask? variable, int? layer1 = null, int? layer2 = null, int? layer3 = null, int? layer4 = null, int? layer5 = null) {
            if (variable != null) {
                return (LayerMask) variable;
            }

            variable = 0;
            ApplyLayerMask(ref variable, layer1);
            ApplyLayerMask(ref variable, layer2);
            ApplyLayerMask(ref variable, layer3);
            ApplyLayerMask(ref variable, layer4);
            ApplyLayerMask(ref variable, layer5);

            return (LayerMask) variable;
        }

        private static void ApplyLayerMask(ref LayerMask? variable, int? layer) {
            if (layer.HasValue) {
                variable |= 1 << layer;
            }
        }

        private static int LazyLoadLayer(ref int? variable, string layer) {
            if (variable.HasValue) {
                return (int) variable;
            }

            return (int) (variable = LayerMask.NameToLayer(layer));
        }
    }
}
