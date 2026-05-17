using Photon.Deterministic;
using System;

namespace Quantum {
    [Serializable]
    public unsafe partial struct GameRules {

        public readonly bool IsLivesEnabled => Lives > 0;
        public readonly bool IsTimerEnabled => TimerMinutes > 0;

        public readonly bool IsCoinItemDisabled(Frame f, AssetRef<CoinItemAsset> coinItem) {
            if (f.TryResolveDictionary(CoinItemCustomSpawnWeights, out var customWeights)) {
                if (customWeights.TryGetValue(coinItem, out FP customWeight)) {
                    return customWeight <= FP.UseableMin;
                }
            }
            return false;
        }
    }
}