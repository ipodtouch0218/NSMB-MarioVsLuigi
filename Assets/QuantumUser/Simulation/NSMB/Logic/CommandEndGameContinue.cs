using Photon.Deterministic;

namespace Quantum {
    public class CommandEndGameContinue : DeterministicCommand, ILobbyCommand {
        public override void Serialize(BitStream stream) {
            // Sorry, nothing.
        }

        public unsafe void Execute(Frame f, PlayerRef sender, PlayerData* playerData) {
            int minFrames = (f.UpdateRate / 2) + 1;
            if (f.Global->GameState != GameState.Ended
                || playerData->VotedToContinue
                || f.Global->GameStartFrames <= minFrames) {
                return;
            }

            int players = f.ComponentCount<PlayerData>();

            f.Global->GameStartFrames = (ushort) FPMath.Max(f.Global->GameStartFrames - ((15 * f.UpdateRate) / FPMath.Max(1, players)), minFrames);
            playerData->VotedToContinue = true;
            f.Events.PlayerDataChanged(sender);
        }
    }
}