using Photon.Deterministic;

namespace Quantum {
    public class CommandSetInSettings : DeterministicCommand {

        public bool InSettings;

        public override void Serialize(BitStream stream) {
            stream.Serialize(ref InSettings);
        }
    }
}