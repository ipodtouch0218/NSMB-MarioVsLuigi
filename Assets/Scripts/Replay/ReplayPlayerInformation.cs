using Quantum;
using System.IO;

namespace NSMB.Replay { 

    public struct ReplayPlayerInformation {
        public string Username;
        public int FinalObjectiveCount;
        public byte Team;
        public byte Character;
        public PlayerRef PlayerRef;

        public void Serialize(BinaryWriter writer) {
            writer.Write(Username);
            writer.Write(FinalObjectiveCount);
            writer.Write(Team);
            writer.Write(Character);
            writer.Write(PlayerRef);
        }

        public static ReplayPlayerInformation Deserialize(BinaryReader reader) {
            return new ReplayPlayerInformation {
                Username = reader.ReadString(),
                FinalObjectiveCount = reader.ReadInt32(),
                Team = reader.ReadByte(),
                Character = reader.ReadByte(),
                PlayerRef = reader.ReadInt32(),
            };
        }
    }
}
