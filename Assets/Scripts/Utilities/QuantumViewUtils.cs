using NSMB.Replay;
using NSMB.UI.Game;
using Photon.Deterministic;
using Quantum;
using System.Linq;
using UnityEngine;

namespace NSMB.Utilities {
    public static class QuantumViewUtils {

        private static readonly SoundEffect[] ComboSounds = {
            SoundEffect.Enemy_Shell_Kick,
            SoundEffect.Enemy_Shell_Combo1,
            SoundEffect.Enemy_Shell_Combo2,
            SoundEffect.Enemy_Shell_Combo3,
            SoundEffect.Enemy_Shell_Combo4,
            SoundEffect.Enemy_Shell_Combo5,
            SoundEffect.Enemy_Shell_Combo6,
            SoundEffect.Enemy_Shell_Combo7,
        };

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

        public static T FindAssetOrDefault<T>(AssetRef<T> assetRef, AssetRef<T>? def = null) where T : AssetObject {
            if (!QuantumUnityDB.TryGetGlobalAsset(assetRef, out T result)) {
                if (def.HasValue) {
                    QuantumUnityDB.TryGetGlobalAsset(def.Value, out result);
                } else {
                    var assets = AssetRepository<T>.AllAssets;
                    if (assets.Count > 0) {
                        result = assets[0];
                    }
                }
            }
            return result;
        }

        public static SoundEffect GetComboSoundEffect(int combo) {
            return ComboSounds[Mathf.Clamp(combo, 0, ComboSounds.Length - 1)];
        }

        public static unsafe bool TryGetHostPlayerSlot(QuantumGame game, out int slot) {
            Frame f = game.Frames.Predicted;
            int index = game.GetLocalPlayers().IndexOf(f.Global->Host);
            if (index == -1) {
                slot = -1;
                return false;
            } else {
                slot = game.GetLocalPlayerSlots()[index];
                return true;
            }
        }
    }
}
