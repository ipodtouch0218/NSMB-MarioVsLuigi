using Photon.Deterministic;
using Quantum;
using System;
using UnityEngine;
using UnityEngine.Tilemaps;

public class StageTile : AssetObject {

    public TileBase Tile;
    public TileCollisionData CollisionData;
    public bool IsSlipperyGround, IsSlideableGround;

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

    bool Interact(Frame f, EntityRef entity, InteractionDirection direction, Vector2Int tilePosition, StageTileInstance tileInstance);

    public enum InteractionDirection {
        Up, Down, Left, Right
    }
}
