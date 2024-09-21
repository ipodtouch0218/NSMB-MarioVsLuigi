using Photon.Deterministic;

namespace Quantum {
    public class CommandChangePlayerData : DeterministicCommand {

        public byte Character;
        public byte Skin;
        public byte Team;

        public bool Spectating;
        
        public override void Serialize(BitStream stream) {
            stream.Serialize(ref Character);
            stream.Serialize(ref Skin);
            stream.Serialize(ref Team);
            stream.Serialize(ref Spectating);
        }
    }
}