using Photon.Deterministic;
using System;
using System.Linq;

namespace Quantum {
    public class StageTile : AssetObject {

#if QUANTUM_UNITY
        public UnityEngine.Tilemaps.TileBase Tile;
#endif

        public TileCollisionData CollisionData = TileCollisionData.Default;
        public bool IsSlipperyGround, IsSlideableGround, IsPolygon = true;
        public SoundEffect FootstepSound = SoundEffect.Player_Walk_Grass;
        public ParticleEffect FootstepParticle = ParticleEffect.None;

        [Serializable]
        public struct TileCollisionData : IEquatable<TileCollisionData> {

            public static TileCollisionData Default = new() {
                IsFullTile = true
            };

            public bool IsFullTile;
            public TileShape[] Shapes;

            [Serializable]
            public struct TileShape : IEquatable<TileShape> {
                public FPVector2[] Vertices;

                public readonly bool Equals(TileShape other) {
                    // Uses linq... whatever.
                    return Vertices.SequenceEqual(other.Vertices);
                }
            }

            public readonly bool Equals(TileCollisionData other) {
                return IsFullTile == other.IsFullTile
                    && Shapes.SequenceEqual(other.Shapes);
            }
        }
    }

    public unsafe interface IInteractableTile {
        bool Interact(Frame f, EntityRef entity, InteractionDirection direction, IntVector2 tilePosition, StageTileInstance tileInstance, out bool playBumpSound);
    }
}