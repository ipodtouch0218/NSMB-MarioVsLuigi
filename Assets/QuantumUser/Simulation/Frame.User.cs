using Photon.Deterministic;
using System.Text;

namespace Quantum {
    public unsafe partial class Frame {

        public StageTileInstance* StageTiles;
        public int StageTilesLength;

        partial void FreeUser() {
            if (StageTiles != null) {
                Context.Allocator.Free(StageTiles);
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

        partial void DumpFrameUser(ref string dump) {
            StringBuilder builder = new();
            builder.AppendLine("\n# FRAME.USER STATE");
            builder.Append("StageTiles Array (").Append(StageTilesLength).Append("): ");
            if (StageTilesLength == 0) {
                builder.Append("[]");
            } else {
                builder.AppendLine();
                for (int i = 0; i < StageTilesLength; i++) {
                    builder.Append("  [").Append(i).Append("]: ");
                    if (TryFindAsset(StageTiles[i].Tile, out StageTile tile)) {
                        builder.Append(tile.name);
                    } else {
                        builder.Append(StageTiles[i].Tile.ToString());
                    }
                    builder.Append(" (rot=").Append(StageTiles[i].Rotation).Append(", flags=").Append((byte) StageTiles[i].Flags).Append(")");
                    builder.AppendLine();
                }
            }

            if (Map != null && TryFindAsset(Map.UserAsset, out VersusStageData stage)) {
                int width = stage.TileDimensions.X;
                builder.AppendLine("StageTiles Layout:");
                for (int y = stage.TileDimensions.Y - 1; y >= 0; y--) {
                    for (int x = 0; x < width; x++) {
                        if (TryFindAsset(StageTiles[x + y * width].Tile, out StageTile tile)) {
                            builder.Append(tile.name[0]);
                        } else {
                            builder.Append('.');
                        }
                    }
                    builder.Append('\n');
                }
            }

            dump += builder.ToString();
        }

        partial void CopyFromUser(Frame frame) {
            ReallocStageTiles(frame.StageTilesLength);
            Native.Utils.Copy(StageTiles, frame.StageTiles, StageTileInstance.SIZE * frame.StageTilesLength);
        }

        public void ReallocStageTiles(int newSize) {
            if (StageTilesLength == newSize) {
                return;
            }

            if (StageTiles != null) {
                Context.Allocator.Free(StageTiles);
                StageTiles = null;
            }
            
            if (newSize > 0) {
                StageTiles = (StageTileInstance*) Context.Allocator.AllocAndClear(StageTileInstance.SIZE * newSize, StageTileInstance.ALIGNMENT);
            }

            StageTilesLength = newSize;
        }

        public bool PlayerIsConnected(PlayerRef player) {
            return ResolveDictionary(Global->PlayerDatas).ContainsKey(player);
        }
    }
}