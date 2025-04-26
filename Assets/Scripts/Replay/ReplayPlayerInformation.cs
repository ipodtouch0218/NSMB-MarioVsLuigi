using Quantum;
using System.IO;

namespace NSMB.Replay { 

    public struct ReplayPlayerInformation {
        public string Username;
        public byte FinalStarCount;
        public byte Team;
        public byte Character;
        public PlayerRef PlayerRef;

        public void Serialize(BinaryWriter writer) {
            writer.Write(Username);
            writer.Write(FinalStarCount);
            writer.Write(Team);
            writer.Write(Character);
            writer.Write(PlayerRef);
        }

        public static ReplayPlayerInformation Deserialize(BinaryReader reader) {
            return new ReplayPlayerInformation {
                Username = reader.ReadString(),
                FinalStarCount = reader.ReadByte(),
                Team = reader.ReadByte(),
                Character = reader.ReadByte(),
                PlayerRef = reader.ReadInt32(),
            };
        }
    }
}
