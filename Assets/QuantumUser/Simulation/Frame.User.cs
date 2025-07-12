using System;

namespace Quantum {
    public unsafe partial class Frame {

        public StageTileInstance[] StageTiles;

        partial void InitUser() {
            StageTiles = Array.Empty<StageTileInstance>();
        }

        partial void SerializeUser(FrameSerializer serializer) {
            // Possible desync fix?
            StageTiles ??= Array.Empty<StageTileInstance>();

            serializer.Stream.SerializeArrayLength(ref StageTiles);
            for (int i = 0; i < StageTiles.Length; i++) {
                ref StageTileInstance tile = ref StageTiles[i];
                serializer.Stream.Serialize(ref tile.Tile);

                if (serializer.Stream.Writing) {
                    serializer.Stream.WriteByte((byte) tile.Flags);
                    serializer.Stream.WriteByte((byte) (tile.Rotation >> 8));
                    serializer.Stream.WriteByte((byte) tile.Rotation);
                } else {
                    tile.Flags = (StageTileFlags) serializer.Stream.ReadByte();
                    tile.Rotation = (ushort) (serializer.Stream.ReadByte() << 8);
                    tile.Rotation |= serializer.Stream.ReadByte();
                }
            }
        }

        partial void CopyFromUser(Frame frame) {
            if (StageTiles.Length != frame.StageTiles.Length) {
                StageTiles = new StageTileInstance[frame.StageTiles.Length];
            }
            Array.Copy(frame.StageTiles, StageTiles, frame.StageTiles.Length);
        }
    }
}