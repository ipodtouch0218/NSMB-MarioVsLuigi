using Photon.Deterministic;

namespace Quantum {
    public class CommandPlayerLoaded : DeterministicCommand, ILobbyCommand {
        public override void Serialize(BitStream stream) {
            // Sorry, nothing.
        }

        public unsafe void Execute(Frame f, PlayerRef sender, PlayerData* playerData) {
            if (f.Global->GameState != GameState.WaitingForPlayers) {
                return;
            }

            bool wasLoaded = playerData->IsLoaded;
            playerData->IsLoaded = true;

            if (!wasLoaded) {
                f.Events.PlayerLoaded(f, sender);
            }
        }
    }
}