using Photon.Deterministic;

namespace Quantum {
    public class CommandBanPlayer : DeterministicCommand, ILobbyCommand {

        public PlayerRef Target;

        public override void Serialize(BitStream stream) {
            stream.Serialize(ref Target);

        }
        public unsafe void Execute(Frame f, PlayerRef sender, PlayerData* playerData) {
            if (f.Global->GameState != GameState.PreGameRoom || sender == Target || !playerData->IsRoomHost) {
                // Can't kick
                return;
            }

            // TODO: Also ban player

            f.Events.PlayerKickedFromRoom(f, Target);
            f.Signals.OnPlayerRemoved(Target);
        }
    }
}