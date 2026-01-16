using NSMB.Addons;
using NSMB.Replay;
using NSMB.UI.Game;
using Photon.Deterministic;
using Quantum;
using Quantum.Core;
using System.Linq;

namespace NSMB.Utilities {
    public static class QuantumViewUtils {

        static QuantumViewUtils() {
            AddonManager.OnAddonLoaded += OnAddonListChanged;
            AddonManager.OnAddonUnloaded += OnAddonListChanged;
        }

        private static Map[] _maps;
        public static Map[] Maps => LazyLoadAssetsOfType(ref _maps);
        private static TeamAsset[] _teams;
        public static TeamAsset[] Teams => LazyLoadAssetsOfType(ref _teams);
        private static CharacterAsset[] _characters;
        public static CharacterAsset[] Characters => LazyLoadAssetsOfType(ref _characters);
        private static PaletteSet[] _palettes;
        public static PaletteSet[] Palettes => LazyLoadAssetsOfType(ref _palettes);


        public static bool IsReplay => QuantumRunner.Default?.Session.IsReplay ?? false;
        public static bool IsReplayFastForwarding => ActiveReplayManager.Instance.IsReplayFastForwarding;

        public static bool FilterOutReplayFastForward(IDeterministicGame game) {
            return !IsReplayFastForwarding;
        }

        public static bool FilterOutReplay(IDeterministicGame game) {
            return !((QuantumGame) game).Session.IsReplay;
        }

        public static bool IsMarioLocal(EntityRef entity) {
            return PlayerElements.AllPlayerElements.Any(pe => pe.Entity == entity);
        }

        public static T FindAssetOrFirst<T>(AssetRef<T> assetRef, T[] possibleValues) where T : AssetObject {
            if (QuantumUnityDB.TryGetGlobalAsset(assetRef, out var result)) {
                return result;
            } else {
                return possibleValues[0];
            }
        }

        public static CharacterAsset FindAssetOrFirst(AssetRef<CharacterAsset> assetRef) {
            return FindAssetOrFirst(assetRef, Characters);
        }

        private static void OnAddonListChanged(LoadedAddon la) {
            _maps = null;
            _teams = null;
            _characters = null;
            _palettes = null;
        }

        private static T[] LazyLoadAssetsOfType<T>(ref T[] arr) where T : AssetObject {
            return arr ??= 
                QuantumUnityDB.Global.GetAssets(new() { Type = typeof(T) })
                    .Cast<T>()
                    .ToArray();
        }
    }
}
