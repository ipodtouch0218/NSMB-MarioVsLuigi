using Unity.Collections.LowLevel.Unsafe;

namespace Quantum {
    public unsafe partial class Frame {

        public StageTileInstance* StageTiles;
        public int StageTilesLength;

        partial void FreeUser() {
            if (StageTiles != null) {
                UnsafeUtility.Free(StageTiles, Unity.Collections.Allocator.Persistent);
                StageTiles = null;
            }
        }

        partial void SerializeUser(FrameSerializer serializer) {
            var stream = serializer.Stream;

            // Tilemap
            if (stream.Writing) {
                stream.WriteInt(StageTilesLength);
            } else {
                int newLength = stream.ReadInt();
                ReallocStageTiles(newLength);
            }
            for (int i = 0; i < StageTilesLength; i++) {
                StageTileInstance.Serialize(StageTiles + i, serializer);
            }
        }

        partial void CopyFromUser(Frame frame) {
            ReallocStageTiles(frame.StageTilesLength);
            UnsafeUtility.MemCpy(StageTiles, frame.StageTiles, StageTileInstance.SIZE * frame.StageTilesLength);
        }

        public void ReallocStageTiles(int newSize) {
            if (StageTilesLength == newSize) {
                return;
            }

            if (StageTiles != null) {
                UnsafeUtility.Free(StageTiles, Unity.Collections.Allocator.Persistent);
                StageTiles = null;
            }
            
            if (newSize > 0) {
                StageTiles = (StageTileInstance*) UnsafeUtility.Malloc(StageTileInstance.SIZE * newSize, StageTileInstance.ALIGNMENT, Unity.Collections.Allocator.Persistent);
            }

            StageTilesLength = newSize;
        }
    }
}