using Photon.Deterministic;

namespace Quantum {
    public class CommandSendChatMessage : DeterministicCommand, ILobbyCommand {

        public string Message;

        public override void Serialize(BitStream stream) {
            stream.Serialize(ref Message);
        }

        public unsafe void Execute(Frame f, PlayerRef sender, PlayerData* playerData) {
            if (f.Global->GameState != GameState.PreGameRoom
                || !playerData->CanSendChatMessage(f)) {
                return;
            }

            playerData->LastChatMessage = f.Number;
            f.Events.PlayerSentChatMessage(f, sender, Message);
        }
    }
}