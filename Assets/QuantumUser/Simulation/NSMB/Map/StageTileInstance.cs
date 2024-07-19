using Photon.Deterministic;
using System;

namespace Quantum {
    [Serializable]
    public unsafe partial struct StageTileInstance {

        public FPVector2[][] GetWorldPolygons(Frame f, FPVector2 worldPos) {
            return GetWorldPolygons(f.FindAsset(Tile), worldPos);
        }

        public FPVector2[][] GetWorldPolygons(StageTile stageTile, FPVector2 worldPos) {
            if (!stageTile || stageTile.CollisionData.Shapes == null) {
                return Array.Empty<FPVector2[]>();
            }

            FPVector2[][] polygons = new FPVector2[stageTile.CollisionData.Shapes.Length][];

            for (int shape = 0; shape < polygons.Length; shape++) {
                polygons[shape] = new FPVector2[stageTile.CollisionData.Shapes[shape].Vertices.Length];

                for (int point = 0; point < polygons[shape].Length; point++) {
                    FPVector2 p = stageTile.CollisionData.Shapes[shape].Vertices[point];
                    // Scale
                    p = FPVector2.Scale(p, Scale) / 2;
                    // Rotate
                    p = RotateAroundOrigin(p, Rotation);
                    // Translate
                    p += worldPos;
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