using Photon.Deterministic;

namespace Quantum {
    public class CommandUnbanPlayer : DeterministicCommand, ILobbyCommand {

        public string TargetUserId;

        public override void Serialize(BitStream stream) {
            stream.Serialize(ref TargetUserId);
        }

        public unsafe void Execute(Frame f, PlayerRef sender, PlayerData* playerData) {
            if (f.Global->GameState != GameState.PreGameRoom || !playerData->IsRoomHost) {
                return;
            }

            var bans = f.ResolveList(f.Global->BannedPlayerIds);
            for (int i = bans.Count - 1; i >= 0; i--) {
                if (bans[i].UserId == TargetUserId) {
                    f.Events.PlayerUnbanned(bans[i]);
                    bans.RemoveAt(i);
                }
            }
        }
    }
}