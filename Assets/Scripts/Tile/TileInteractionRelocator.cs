using UnityEngine;

using NSMB.Entities;

namespace NSMB.Tiles {

    [CreateAssetMenu(fileName = "InteractionRelocator", menuName = "ScriptableObjects/Tiles/InteractionRelocator", order = 8)]
    public class TileInteractionRelocator : InteractableTile {

        public Vector2Int offset;

        public override bool Interact(BasicEntity interacter, InteractionDirection direction, Vector3 worldLocation, out bool bumpSound) {
            Vector2Int tileLocation = Utils.Utils.WorldToTilemapPosition(worldLocation);

            if (Utils.Utils.GetTileAtTileLocation(tileLocation + offset) is InteractableTile tile)
                return tile.Interact(interacter, direction, (Vector2) worldLocation + ((Vector2) offset * 0.5f), out bumpSound);

            bumpSound = true;
            return false;
        }
    }
}
