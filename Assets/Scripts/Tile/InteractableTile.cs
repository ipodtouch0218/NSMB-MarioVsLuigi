using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public abstract class InteractableTile : AnimatedTile {

    public abstract bool Interact(MonoBehaviour interacter, InteractionDirection direction, Vector2 location);

    public enum InteractionDirection {
        Up, Down, Left, Right
    }
}