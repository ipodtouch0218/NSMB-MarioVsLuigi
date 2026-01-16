using Photon.Deterministic;

namespace Quantum {
    public class CommandChangeHost : DeterministicCommand, ILobbyCommand {

        public PlayerRef Target;

        public override void Serialize(BitStream stream) {
            stream.Serialize(ref Target);
        }

        public unsafe void Execute(Frame f, PlayerRef sender, PlayerData* playerData) {
            if (f.Global->GameState != GameState.PreGameRoom || !playerData->IsRoomHost) {
                // Only the host can give it to another player.
                return;
            }

            var newHostPlayerData = QuantumUtils.GetPlayerData(f, Target);
            if (newHostPlayerData == null) {
                return;
            }

            playerData->IsRoomHost = false;
            newHostPlayerData->IsRoomHost = true;
            f.Global->Host = Target;
            f.Events.HostChanged(Target);
        }
    }
}