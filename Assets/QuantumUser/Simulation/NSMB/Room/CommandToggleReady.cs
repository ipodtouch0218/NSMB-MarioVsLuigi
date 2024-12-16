using Photon.Deterministic;

namespace Quantum {
    public class CommandToggleReady : DeterministicCommand, ILobbyCommand {
        public override void Serialize(BitStream stream) {
            // Sorry, nothing.
        }

        public unsafe void Execute(Frame f, PlayerRef sender, PlayerData* playerData) {
            if (f.Global->GameState != GameState.PreGameRoom) {
                return;
            }

            playerData->IsReady = !playerData->IsReady;
            f.Events.PlayerDataChanged(f, sender);
        }
    }
}