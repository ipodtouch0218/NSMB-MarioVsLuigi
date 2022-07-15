using NSMB.Utils;
using UnityEngine;

[CreateAssetMenu(fileName = "InteractionRelocator", menuName = "ScriptableObjects/Tiles/InteractionRelocator", order = 8)]
public class TileInteractionRelocator : InteractableTile {

    public Vector3Int offset;

    public override bool Interact(MonoBehaviour interacter, InteractionDirection direction, Vector3 worldLocation) {
        Vector3Int tileLocation = Utils.WorldToTilemapPosition(worldLocation);
        if (Utils.GetTileAtTileLocation(tileLocation + offset) is InteractableTile tile)
            return tile.Interact(interacter, direction, worldLocation + ((Vector3) offset / 2f));

        return false;
    }


}