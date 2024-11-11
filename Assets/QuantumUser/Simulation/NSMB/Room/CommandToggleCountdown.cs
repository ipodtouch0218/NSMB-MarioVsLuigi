using Photon.Deterministic;

namespace Quantum {
    public class CommandToggleCountdown : DeterministicCommand, ILobbyCommand {
        public override void Serialize(BitStream stream) {
            // Sorry, nothing.
        }

        public unsafe void Execute(Frame f, PlayerRef sender, PlayerData* playerData) {
            if (f.Global->GameState != GameState.PreGameRoom && !playerData->IsRoomHost) {
                // Only the host can start the countdown.
                return;
            }

            bool gameStarting = f.Global->GameStartFrames == 0;
            f.Global->GameStartFrames = (ushort) (gameStarting ? 3 * 60 : 0);
            f.Events.StartingCountdownChanged(f, gameStarting);
        }
    }
}