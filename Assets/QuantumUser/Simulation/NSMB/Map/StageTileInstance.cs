using Photon.Deterministic;
using System;

namespace Quantum {
    [Serializable]
    public unsafe partial struct StageTileInstance : IEquatable<StageTileInstance> {

        public bool HasWorldPolygons(Frame f) {
            return HasWorldPolygons(f.FindAsset(Tile));
        }

        public bool HasWorldPolygons(StageTile stageTile) {
            return stageTile != null && stageTile.CollisionData.Shapes != null && stageTile.CollisionData.Shapes.Length > 0;
        }

        public void GetWorldPolygons(FrameThreadSafe f, Span<FPVector2> vertexBuffer, Span<int> shapeVertexCountBuffer, out StageTile tile, FPVector2? worldPos = null) {
            GetWorldPolygons(f, f.FindAsset<VersusStageData>(f.Map.UserAsset), tile = f.FindAsset(Tile), vertexBuffer, shapeVertexCountBuffer, worldPos ?? FPVector2.Zero);
        }

        public void GetWorldPolygons(FrameThreadSafe f, VersusStageData stage, StageTile stageTile, Span<FPVector2> vertexBuffer, Span<int> shapeVertexCountBuffer, FPVector2? worldPos = null) {
            if (!stageTile) {
                shapeVertexCountBuffer[0] = 0;
                return;
            }

            if (stageTile is TileInteractionRelocator tir) {
                f.TryFindAsset(stage.GetTileRelative((Frame) f, tir.RelocateTo.x, tir.RelocateTo.y).Tile, out stageTile);
            }

            if (!stageTile || stageTile.CollisionData.Shapes == null) {
                shapeVertexCountBuffer[0] = 0;
                return;
            }

            worldPos ??= FPVector2.Zero;

            var polygons = stageTile.CollisionData.Shapes;
            int vertexIndex = 0;

            for (int shapeIndex = 0; shapeIndex < polygons.Length; shapeIndex++) {
                int shapePointCount = polygons[shapeIndex].Vertices.Length;
                shapeVertexCountBuffer[shapeIndex] = shapePointCount;

                for (int point = 0; point < shapePointCount; point++) {
                    int index = point;
                    if (Scale.X < 0 ^ Scale.Y < 0) {
                        // Flipping produced counter-clockwise points;
                        // Invert order for proper normals

                        index = shapePointCount - point - 1;
                    }
                    
                    FPVector2 p = stageTile.CollisionData.Shapes[shapeIndex].Vertices[index];
                    // Scale
                    p = FPVector2.Scale(p, Scale) / 2;
                    // Rotate
                    p = RotateAroundOrigin(p, Rotation);
                    // Translate
                    p += worldPos.Value;

                    vertexBuffer[vertexIndex++] = p;
                }
            }
        }

        private static FPVector2 RotateAroundOrigin(FPVector2 point, FP rotationDegrees) {
            FP rotation = rotationDegrees * FP.Deg2Rad;
            return new(
                point.X * FPMath.Cos(rotation) - point.Y * FPMath.Sin(rotation),
                point.Y * FPMath.Cos(rotation) + point.X * FPMath.Sin(rotation)
            );
        }

        public bool Equals(StageTileInstance other) {
            return Tile == other.Tile && Rotation == other.Rotation && Scale == other.Scale;
        }
    }
}