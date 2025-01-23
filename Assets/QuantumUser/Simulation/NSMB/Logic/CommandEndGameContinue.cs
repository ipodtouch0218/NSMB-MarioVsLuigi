using Photon.Deterministic;

namespace Quantum {
    public class CommandEndGameContinue : DeterministicCommand, ILobbyCommand {
        public override void Serialize(BitStream stream) {
            // Sorry, nothing.
        }

        public unsafe void Execute(Frame f, PlayerRef sender, PlayerData* playerData) {
            int minFrames = 4 * f.UpdateRate;
            if (f.Global->GameState != GameState.Ended
                || playerData->VotedToContinue || playerData->IsSpectator
                || f.Global->GameStartFrames <= minFrames) {
                return;
            }

            int remainingRealPlayers = 0;
            for (int i = 0; i < f.Global->RealPlayers; i++) {
                if (!f.Global->PlayerInfo[i].Disconnected) {
                    remainingRealPlayers++;
                }
            }

            f.Global->GameStartFrames = (ushort) FPMath.Max(f.Global->GameStartFrames - ((10 * f.UpdateRate) / FPMath.Max(1, remainingRealPlayers)), minFrames);
            playerData->VotedToContinue = true;
        }
    }
}