using UnityEngine;
using UnityEngine.Tilemaps;

using NSMB.Utils;

public abstract class InteractableTile : AnimatedTile {

    private static readonly Vector3 BumpOffset = new(0.25f, 0.5f), BumpSize = new(0.45f, 0.1f);
    private static readonly Collider2D[] collisions = new Collider2D[32];

    public abstract bool Interact(BasicEntity interacter, InteractionDirection direction, Vector3 worldLocation);

    public static void Bump(BasicEntity interacter, InteractionDirection direction, Vector3 worldLocation) {
        //check for entities above to bump
        int count = NetworkHandler.Instance.runner.GetPhysicsScene2D().OverlapBox(worldLocation + BumpOffset, BumpSize, 0, collisions);
        for (int i = 0; i < count; i++) {
            Collider2D collider = collisions[i];
            GameObject obj = collider.gameObject;

            if (obj == interacter.gameObject)
                continue;

            if (obj.TryGetComponent(out IBlockBumpable bumpable))
                bumpable.BlockBump(interacter, Utils.WorldToTilemapPosition(worldLocation), direction);
        }
    }

    public enum InteractionDirection {
        Up, Down, Left, Right
    }
}
