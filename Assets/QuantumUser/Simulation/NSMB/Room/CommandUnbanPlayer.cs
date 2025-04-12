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

            /*
            RuntimePlayer targetPlayerData = f.GetPlayerData(Target);
            f.ResolveList(f.Global->BannedPlayerIds).Add(targetPlayerData.UserId);
            f.Events.PlayerKickedFromRoom(Target, true);
            */
        }
    }
}