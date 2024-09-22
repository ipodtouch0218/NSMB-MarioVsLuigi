using System;

namespace Quantum {
    public unsafe partial class Frame {

        public StageTileInstance[] StageTiles;

        partial void InitUser() {
            StageTiles = Array.Empty<StageTileInstance>();
        }

        partial void SerializeUser(FrameSerializer serializer) {
            serializer.Stream.SerializeArrayLength(ref StageTiles);
            for (int i = 0; i < StageTiles.Length; i++) {
                serializer.Stream.Serialize(ref StageTiles[i].Tile);
                serializer.Stream.Serialize(ref StageTiles[i].Rotation);
                serializer.Stream.Serialize(ref StageTiles[i].Scale);
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