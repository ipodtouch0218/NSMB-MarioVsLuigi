using Photon.Deterministic;

namespace Quantum {
    public class CommandStartTyping : DeterministicCommand, ILobbyCommand {
        public override void Serialize(BitStream stream) {
            // Sorry, nothing.
        }
        public unsafe void Execute(Frame f, PlayerRef sender, PlayerData* playerData) {
            if (f.Global->GameState != GameState.PreGameRoom) {
                return;
            }

            f.Events.PlayerStartedTyping(f, sender);
        }
    }
}