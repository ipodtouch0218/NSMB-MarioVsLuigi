using Photon.Deterministic;

namespace Quantum {
    public class CommandChangePlayerData : DeterministicCommand {

        public Changes EnabledChanges;

        public byte Character;
        public byte Skin;
        public byte Team;
        public bool Spectating;

        public override void Serialize(BitStream stream) {
            byte changes = (byte) EnabledChanges;
            stream.Serialize(ref changes);
            EnabledChanges = (Changes) changes;

            stream.Serialize(ref Character);
            stream.Serialize(ref Skin);
            stream.Serialize(ref Team);
            stream.Serialize(ref Spectating);
        }

        public enum Changes : byte {
            Character = 1 << 0,
            Skin = 1 << 1,
            Team = 1 << 2,
            Spectating = 1 << 3,
            All = byte.MaxValue,
        }
    }
}