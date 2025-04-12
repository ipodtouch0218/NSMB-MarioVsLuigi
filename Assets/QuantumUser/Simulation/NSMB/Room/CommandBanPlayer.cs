using Photon.Deterministic;

namespace Quantum {
    public class CommandBanPlayer : DeterministicCommand, ILobbyCommand {

        public PlayerRef Target;

        public override void Serialize(BitStream stream) {
            stream.Serialize(ref Target);

        }
        public unsafe void Execute(Frame f, PlayerRef sender, PlayerData* playerData) {
            if (f.Global->GameState != GameState.PreGameRoom || sender == Target || !playerData->IsRoomHost) {
                // Can't ban
                return;
            }

            RuntimePlayer targetPlayerData = f.GetPlayerData(Target);
            f.ResolveList(f.Global->BannedPlayerIds).Add(new BannedPlayerInfo {
                Nickname = targetPlayerData.PlayerNickname,
                UserId = targetPlayerData.UserId,
            });
            f.Events.PlayerKickedFromRoom(Target, true);
            f.Signals.OnPlayerRemoved(Target);
        }
    }
}