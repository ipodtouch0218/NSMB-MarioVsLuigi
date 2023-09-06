using System;
using UnityEngine;

using Fusion;

namespace NSMB.Extensions {
    public static class FusionExtensions {

        public static bool IsActive(this TickTimer timer, NetworkRunner runner) {
            return !timer.ExpiredOrNotRunning(runner);
        }

        public static float? RemainingRenderTime(this TickTimer timer, NetworkRunner runner) {

            float? timeRemaining = timer.RemainingTime(runner);
            if (!timeRemaining.HasValue)
                return null;

            return Mathf.Max(0, (float) timeRemaining - (runner.Simulation.StateAlpha * runner.DeltaTime));
        }

        public static PlayerData GetPlayerData(this PlayerRef player, NetworkRunner runner) {
            NetworkObject obj = runner.GetPlayerObject(player);
            if (!obj)
                return null;

            return obj.GetComponent<PlayerData>();
        }

        public static PlayerData GetLocalPlayerData(this NetworkRunner runner) {
            try {
                return runner.LocalPlayer.GetPlayerData(runner);
            } catch {}
            return null;
        }

        public static CharacterData GetCharacterData(this PlayerRef player, NetworkRunner runner) {
            return player.GetPlayerData(runner).GetCharacterData();
        }

        public static CharacterData GetCharacterData(this PlayerData data) {
            return ScriptableManager.Instance.characters[data ? data.CharacterIndex : Settings.Instance.generalCharacter];
        }
    }
}
