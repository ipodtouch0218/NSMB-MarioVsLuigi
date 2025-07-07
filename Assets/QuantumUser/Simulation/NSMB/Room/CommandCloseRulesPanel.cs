using Photon.Deterministic;

namespace Quantum {
    public class CommandCloseRulesPanel : DeterministicCommand, ILobbyCommand {

        public override void Serialize(BitStream stream) {
            // Sorry, nothing
        }

        public unsafe void Execute(Frame f, PlayerRef sender, PlayerData* playerData) {
            if (playerData->IsRoomHost) {
                
            }
        }
    }
}