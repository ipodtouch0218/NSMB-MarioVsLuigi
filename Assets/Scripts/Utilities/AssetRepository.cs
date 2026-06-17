using NSMB.Addons;
using Quantum;
using System.Collections.Generic;
using System.Linq;

namespace NSMB.Utilities {
    public static class AssetRepository<T> where T : AssetObject {

        static AssetRepository() {
            AddonManager.OnAddonLoaded += OnAddonListChanged;
            AddonManager.OnAddonUnloaded += OnAddonListChanged;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged += (_) => {
                _allAssetRefs = null;
                _allAssets = null;
            };
#endif
        }

        private static List<AssetRef<T>> _allAssetRefs;
        public static IReadOnlyList<AssetRef<T>> AllAssetRefs {
            get {
                if (_allAssetRefs == null) {
                    Load();
                }

                return _allAssetRefs;
            }
        }

        private static List<T> _allAssets;
        public static IReadOnlyList<T> AllAssets {
            get {
                if (_allAssets == null) {
                    Load();
                }

                return _allAssets;
            }
        }

        public static void Load() {
            var query = 
                QuantumUnityDB.Global.FindAssetGuids(new AssetObjectQuery { Type = typeof(T) })
                    .Select(ag => new AssetRef<T>(ag))
                    .Select(QuantumUnityDB.GetGlobalAsset);

            if (typeof(IOrderedAsset).IsAssignableFrom(typeof(T))) {
                query = query.OrderBy(asset => ((IOrderedAsset) asset).Order);
            }

            _allAssets = query.ToList();
            _allAssetRefs = query.Select(asset => (AssetRef<T>) asset).ToList();
        }

        public static void Invalidate() {
            _allAssetRefs = null;
            _allAssets = null;
        }

        private static void OnAddonListChanged(LoadedAddon _) {
            Invalidate();
        }
    }
}