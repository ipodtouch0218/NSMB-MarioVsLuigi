using UnityEngine;

namespace NSMB.Utils {
    public static class Layers {

        private static int? _maskAnyGround, _maskOnlyGround;
        public static int MaskAnyGround { get => LazyLoadMask(ref _maskAnyGround, "Ground", "Semisolids", "IceBlock"); }
        public static int MaskOnlyGround { get => LazyLoadMask(ref _maskOnlyGround, "Ground"); }

        private static int? _layerGround, _layerHitsNothing, _layerDefault, _layerPassthrough, _layerLooseCoin;
        public static int LayerGround { get => LazyLoadLayer(ref _layerGround, "Ground"); }
        public static int LayerHitsNothing { get => LazyLoadLayer(ref _layerHitsNothing, "HitsNothing"); }
        public static int LayerDefault { get => LazyLoadLayer(ref _layerDefault, "Default"); }
        public static int LayerPassthrough { get => LazyLoadLayer(ref _layerPassthrough, "PlayerPassthrough"); }
        public static int LayerLooseCoin { get => LazyLoadLayer(ref _layerLooseCoin, "LooseCoin"); }

        private static int LazyLoadMask(ref int? variable, params string[] layers) {
            if (variable != null)
                return (int) variable;

            return (int) (variable = LayerMask.GetMask(layers));
        }

        private static int LazyLoadLayer(ref int? variable, string layer) {
            if (variable != null)
                return (int) variable;

            return (int) (variable = LayerMask.NameToLayer(layer));
        }
    }
}