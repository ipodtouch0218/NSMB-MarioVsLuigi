using Photon.Deterministic;
using System;

namespace Quantum {
    [Serializable]
    public unsafe partial struct StageTileInstance {

        public FPVector2[][] GetWorldPolygons(Frame f, FPVector2? worldPos = null) {
            return GetWorldPolygons(f.FindAsset(Tile), worldPos ?? FPVector2.Zero);
        }

        public FPVector2[][] GetWorldPolygons(StageTile stageTile, FPVector2? worldPos = null) {
            if (!stageTile || stageTile.CollisionData.Shapes == null) {
                return Array.Empty<FPVector2[]>();
            }

            worldPos ??= FPVector2.Zero;
            FPVector2[][] polygons = new FPVector2[stageTile.CollisionData.Shapes.Length][];

            for (int shape = 0; shape < polygons.Length; shape++) {
                polygons[shape] = new FPVector2[stageTile.CollisionData.Shapes[shape].Vertices.Length];

                int shapePointCount = polygons[shape].Length;
                for (int point = 0; point < shapePointCount; point++) {
                    int index = point;
                    if (Scale.X < 0 ^ Scale.Y < 0) {
                        // Flipping produced counter-clockwise points;
                        // Invert order for proper normals
                        index = shapePointCount - point - 1;
                    }

                    FPVector2 p = stageTile.CollisionData.Shapes[shape].Vertices[index];
                    // Scale
                    p = FPVector2.Scale(p, Scale) / 2;
                    // Rotate
                    p = RotateAroundOrigin(p, Rotation);
                    // Translate
                    p += worldPos.Value;
                    polygons[shape][point] = p;
                }
            }

            return polygons;
        }

        private static FPVector2 RotateAroundOrigin(FPVector2 point, FP rotationDegrees) {
            FP rotation = rotationDegrees * FP.Deg2Rad;
            return new(
                point.X * FPMath.Cos(rotation) - point.Y * FPMath.Sin(rotation),
                point.Y * FPMath.Cos(rotation) + point.X * FPMath.Sin(rotation)
            );
        }
    }
}