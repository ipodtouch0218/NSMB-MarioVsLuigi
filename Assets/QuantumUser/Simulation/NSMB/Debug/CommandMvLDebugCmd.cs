#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Photon.Deterministic;

namespace Quantum {

    public class CommandMvLDebugCmd : DeterministicCommand {

        public DebugCommand CommandId;
        public AssetRef<EntityPrototype> SpawnData;

        public override void Serialize(BitStream stream) {
            if (stream.Writing) {
                stream.WriteByte((byte) CommandId);
            } else {
                CommandId = (DebugCommand) stream.ReadByte();
            }
            stream.Serialize(ref SpawnData);
        }

        public enum DebugCommand : byte {
            SpawnEntity,
            KillSelf,
            FreezeSelf,
        }
    }
}
#endif