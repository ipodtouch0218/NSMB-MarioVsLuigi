using Photon.Deterministic;

namespace Quantum {
    public class CommandChangeHost : DeterministicCommand {

        public PlayerRef NewHost;

        public override void Serialize(BitStream stream) {
            stream.Serialize(ref NewHost);
        }
    }
}