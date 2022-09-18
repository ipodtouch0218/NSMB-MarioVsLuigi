using Fusion;

namespace NSMB.Extensions {
    public static class FusionExtensions {

        public static PlayerData GetPlayerData(this PlayerRef player, NetworkRunner runner) {
            NetworkObject obj = runner.GetPlayerObject(player);
            if (!obj)
                return null;

            return obj.GetComponent<PlayerData>();
        }
    }
}
