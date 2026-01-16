using Photon.Deterministic;

namespace Quantum {
    public class CommandKickPlayer : DeterministicCommand, ILobbyCommand {

        public PlayerRef Target;

        public override void Serialize(BitStream stream) {
            stream.Serialize(ref Target);

        }
        public unsafe void Execute(Frame f, PlayerRef sender, PlayerData* playerData) {
            if (f.Global->GameState != GameState.PreGameRoom
                || !playerData->IsRoomHost
                || sender == Target
                || !f.PlayerIsConnected(Target)) {
                // Can't kick
                return;
            }

            f.Events.PlayerKickedFromRoom(Target, false);
            f.Signals.OnPlayerRemoved(Target);
        }
    }
}