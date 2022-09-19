using Fusion;

namespace NSMB.Extensions {
    public static class FusionExtensions {

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
