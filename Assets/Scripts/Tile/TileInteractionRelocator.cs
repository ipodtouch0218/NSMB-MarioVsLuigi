using NSMB.Utils;
using UnityEngine;

[CreateAssetMenu(fileName = "InteractionRelocator", menuName = "ScriptableObjects/Tiles/InteractionRelocator", order = 8)]
public class TileInteractionRelocator : InteractableTile {

    public Vector2Int offset;

    public override bool Interact(BasicEntity interacter, InteractionDirection direction, Vector3 worldLocation) {
        Vector2Int tileLocation = Utils.WorldToTilemapPosition(worldLocation);
        if (Utils.GetTileAtTileLocation(tileLocation + offset) is InteractableTile tile)
            return tile.Interact(interacter, direction, (Vector2) worldLocation + ((Vector2) offset * 0.5f));

        return false;
    }


}