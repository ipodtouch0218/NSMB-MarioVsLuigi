using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

using Fusion;

namespace NSMB.Extensions {
    public static class FusionExtensions {

        private static readonly Dictionary<string, string> SPECIAL_PLAYERS = new() {
            ["cf03abdb5d2ef1b6f0d30ae40303936f9ab22f387f8a1072e2849c8292470af1"] = "ipodtouch0218",
            ["d5ba21667a5da00967cc5ebd64c0d648e554fb671637adb3d22a688157d39bf6"] = "mindnomad",
            ["95962949aacdbb42a6123732dabe9c7200ded59d7eeb39c889067bafeebecc72"] = "MPS64",
            ["7e9c6f2eaf0ce11098c8a90fcd9d48b13017667e33d09d0cc5dfe924f3ead6c1"] = "Fawndue",
        };

        public static bool IsServer(this PlayerRef player) {
            return NetworkHandler.Instance.runner.GetPlayerRtt(player) == 0;
        }
        public static bool HasRainbowName(this PlayerRef player) {
            PlayerData data = player.GetPlayerData(NetworkHandler.Instance.runner);
            string userId = data?.GetUserId();
            if (!data || userId == null)
                return false;

            byte[] bytes = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(userId));
            StringBuilder sb = new();
            foreach (byte b in bytes)
                sb.Append(b.ToString("X2"));

            string hash = sb.ToString().ToLower();
            return SPECIAL_PLAYERS.ContainsKey(hash) && data.GetNickname(false) == SPECIAL_PLAYERS[hash];
        }

        public static PlayerData GetPlayerData(this PlayerRef player, NetworkRunner runner) {
            NetworkObject obj = runner.GetPlayerObject(player);
            if (!obj)
                return null;

            return obj.GetComponent<PlayerData>();
        }

        public static PlayerData GetLocalPlayerData(this NetworkRunner runner) {
            return runner.LocalPlayer.GetPlayerData(runner);
        }

        public static CharacterData GetCharacterData(this PlayerRef player, NetworkRunner runner) {
            return runner.GetLocalPlayerData().GetCharacterData();
        }

        public static CharacterData GetCharacterData(this PlayerData data) {
            return GlobalController.Instance.characters[data.CharacterIndex];
        }
    }
}
