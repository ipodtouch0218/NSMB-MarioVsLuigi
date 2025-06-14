using Photon.Deterministic;
using System;

namespace Quantum {
    [Serializable]
    public unsafe partial struct StageTileInstance : IEquatable<StageTileInstance> {

        public bool HasWorldPolygons(Frame f) {
            return Tile != default && HasWorldPolygons(f.FindAsset(Tile));
        }
        
        public bool HasWorldPolygons(StageTile stageTile) {
            return Tile != default && stageTile != null && ((stageTile.CollisionData.Shapes != null && stageTile.CollisionData.Shapes.Length > 0) || stageTile.CollisionData.IsFullTile);
        }
        
        public bool GetWorldPolygons(FrameThreadSafe f, Span<FPVector2> vertexBuffer, Span<int> shapeVertexCountBuffer, out StageTile tile, FPVector2? worldPos = null) {
            return GetWorldPolygons(f, f.FindAsset<VersusStageData>(f.Map.UserAsset), vertexBuffer, shapeVertexCountBuffer, out tile, worldPos ?? FPVector2.Zero);
        }

        public bool GetWorldPolygons(FrameThreadSafe f, VersusStageData stage, Span<FPVector2> vertexBuffer, Span<int> shapeVertexCountBuffer, out StageTile tile, FPVector2? worldPos = null) {
            if (Tile == default) {
                tile = null;
                return false;
            }
            return GetWorldPolygons(f, stage, tile = f.FindAsset(Tile), vertexBuffer, shapeVertexCountBuffer, worldPos ?? FPVector2.Zero);
        }

        public bool GetWorldPolygons(FrameThreadSafe f, VersusStageData stage, StageTile stageTile, Span<FPVector2> vertexBuffer, Span<int> shapeVertexCountBuffer, FPVector2? worldPos = null) {
            if (stageTile == null) {
                shapeVertexCountBuffer[0] = 0;
                return false;
            }

            if (stageTile is TileInteractionRelocator tir) {
                if (!f.TryFindAsset(stage.GetTileRelative((Frame) f, tir.RelocateTo).Tile, out stageTile)) {
                    return false;
                }
            }

            if (stageTile.CollisionData.Shapes == null || stageTile.CollisionData.IsFullTile) {
                shapeVertexCountBuffer[0] = 0;
                return true;
            }

            worldPos ??= FPVector2.Zero;

            var polygons = stageTile.CollisionData.Shapes;
            int vertexIndex = 0;

            for (int shapeIndex = 0; shapeIndex < polygons.Length; shapeIndex++) {
                int shapePointCount = polygons[shapeIndex].Vertices.Length;
                shapeVertexCountBuffer[shapeIndex] = shapePointCount;

                for (int point = 0; point < shapePointCount; point++) {
                    int index = point;
                    if (Flags.HasFlag(StageTileFlags.MirrorX) ^ Flags.HasFlag(StageTileFlags.MirrorY)) {
                        // Flipping produced counter-clockwise points;
                        // Invert order for proper normals

                        index = shapePointCount - point - 1;
                    }
                    
                    FPVector2 p = stageTile.CollisionData.Shapes[shapeIndex].Vertices[index];
                    // Scale
                    FPVector2 scale = FPVector2.One;
                    if (Flags.HasFlag(StageTileFlags.MirrorX)) {
                        scale.X *= -1;
                    }
                    if (Flags.HasFlag(StageTileFlags.MirrorY)) {
                        scale.Y *= -1;
                    }
                    p = FPVector2.Scale(p, scale) / 2;
                    // Rotate
#if OLD
                    p = RotateAroundOrigin(p, Rotation / ((FP) ushort.MaxValue / 360));
#else
                    p = FPVector2.Rotate(p, Rotation / ((FP) ushort.MaxValue / FP.PiTimes2));
#endif
                    // Translate
                    p += worldPos.Value;

                    vertexBuffer[vertexIndex++] = p;
                }
            }
            return true;
        }

        private static FPVector2 RotateAroundOrigin(FPVector2 point, FP rotationDegrees) {
            FP rotation = rotationDegrees * FP.Deg2Rad;
            return new(
                point.X * FPMath.Cos(rotation) - point.Y * FPMath.Sin(rotation),
                point.Y * FPMath.Cos(rotation) + point.X * FPMath.Sin(rotation)
            );
        }

        public bool Equals(StageTileInstance other) {
            return Tile == other.Tile && Flags == other.Flags;
        }
    }
}