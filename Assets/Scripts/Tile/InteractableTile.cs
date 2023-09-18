using UnityEngine;
using UnityEngine.Tilemaps;

using NSMB.Entities;

namespace NSMB.Tiles {
    public abstract class InteractableTile : AnimatedTile {

        //---Static Variables
        private static readonly Vector3 BumpOffset = new(0.25f, 0.5f), BumpSize = new(0.45f, 0.1f);
        private static readonly Collider2D[] CollisionBuffer = new Collider2D[32];

        public abstract bool Interact(BasicEntity interacter, InteractionDirection direction, Vector3 worldLocation, out bool playBumpSound);

        public static void Bump(BasicEntity interacter, InteractionDirection direction, Vector3 worldLocation) {
            // Check for entities above to bump
            int count = NetworkHandler.Runner.GetPhysicsScene2D().OverlapBox(worldLocation + BumpOffset, BumpSize, 0, CollisionBuffer);
            for (int i = 0; i < count; i++) {
                Collider2D collider = CollisionBuffer[i];
                GameObject obj = collider.gameObject;

#pragma warning disable CS0252
                if (obj.GetComponentInParent<IBlockBumpable>() is IBlockBumpable bumpable && bumpable != interacter)
                    bumpable.BlockBump(interacter, Utils.Utils.WorldToTilemapPosition(worldLocation), direction);
#pragma warning restore CS0252
            }
        }
    }

    public enum InteractionDirection {
        Up = 1 << 0,
        Down = 1 << 1,
        Left = 1 << 2,
        Right = 1 << 3,
    }
}
