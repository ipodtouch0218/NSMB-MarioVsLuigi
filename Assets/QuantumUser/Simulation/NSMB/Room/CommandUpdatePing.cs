using Photon.Deterministic;

namespace Quantum {
    public class CommandUpdatePing : DeterministicCommand {

        public int PingMs;

        public override void Serialize(BitStream stream) {
            stream.Serialize(ref PingMs);
        }
    }
}