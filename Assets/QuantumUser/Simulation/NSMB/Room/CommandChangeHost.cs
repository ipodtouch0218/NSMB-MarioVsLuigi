using Photon.Deterministic;

namespace Quantum {
    public class CommandChangeHost : DeterministicCommand, ILobbyCommand {

        public PlayerRef NewHost;

        public override void Serialize(BitStream stream) {
            stream.Serialize(ref NewHost);
        }

        public unsafe void Execute(Frame f, PlayerRef sender, PlayerData* playerData) {
            if (f.Global->GameState != GameState.PreGameRoom && !playerData->IsRoomHost) {
                // Only the host can give it to another player.
                return;
            }

            var newHostPlayerData = QuantumUtils.GetPlayerData(f, NewHost);
            if (newHostPlayerData == null) {
                return;
            }

            playerData->IsRoomHost = false;
            newHostPlayerData->IsRoomHost = true;
            f.Events.HostChanged(f, NewHost);
        }
    }
}