using Photon.Deterministic;

namespace Quantum {
    public class CommandSendChatMessage : DeterministicCommand {

        public string Message;

        public override void Serialize(BitStream stream) {
            stream.Serialize(ref Message);
        }
    }
}