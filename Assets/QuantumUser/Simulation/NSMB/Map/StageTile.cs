using Photon.Deterministic;
using Quantum;
using System;
using UnityEngine;
using UnityEngine.Tilemaps;

public class StageTile : AssetObject {

#if QUANTUM_UNITY
    public TileBase Tile;
#endif 
    public TileCollisionData CollisionData;
    public bool IsSlipperyGround, IsSlideableGround, IsPolygon = true;
    public SoundEffect FootstepSound = SoundEffect.Player_Walk_Grass;
    public ParticleEffect FootstepParticle = ParticleEffect.None;

    [Serializable]
    public struct TileCollisionData {
        public TileShape[] Shapes;

        [Serializable]
        public struct TileShape {
            public FPVector2[] Vertices;
        }
    }
}

public unsafe interface IInteractableTile {

    bool Interact(Frame f, EntityRef entity, InteractionDirection direction, Vector2Int tilePosition, StageTileInstance tileInstance, out bool playBumpSound);

    public enum InteractionDirection {
        Up, Down, Left, Right
    }
}
