using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public abstract class InteractableTile : AnimatedTile {

    protected Vector3 bumpOffset = new Vector3(0, 0.5f, 0);
    public abstract bool Interact(MonoBehaviour interacter, InteractionDirection direction, Vector3 worldLocation);

    public enum InteractionDirection {
        Up, Down, Left, Right
    }
}