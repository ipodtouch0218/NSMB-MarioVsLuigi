using Photon.Deterministic;

namespace Quantum {
    public class CommandToggleCountdown : DeterministicCommand, ILobbyCommand {
        public override void Serialize(BitStream stream) {
            // Sorry, nothing.
        }

        public unsafe void Execute(Frame f, PlayerRef sender, PlayerData* playerData) {
            if (f.Global->GameState != GameState.PreGameRoom
                || !playerData->IsRoomHost(f)) {
                // Only the host can start the countdown.
                return;
            }

            if (f.Global->IsStartGameCountdownActive) {
                if (!QuantumUtils.IsGameStartable(f)) {
                    return;
                }
                f.Global->GameStartFrames = 0;
                f.Global->IsStartGameCountdownActive = false;
            } else {
                f.Global->GameStartFrames = (ushort) (3 * f.UpdateRate);
                f.Global->IsStartGameCountdownActive = true;
            }
            f.Events.StartingCountdownChanged(f.Global->IsStartGameCountdownActive);
        }
    }
}