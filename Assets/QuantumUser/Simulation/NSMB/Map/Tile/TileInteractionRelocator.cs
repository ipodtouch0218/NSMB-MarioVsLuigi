using Quantum;
using UnityEngine;

public class TileInteractionRelocator : StageTile, IInteractableTile {

    public Vector2Int RelocateTo;

    public bool Interact(Frame f, EntityRef entity, IInteractableTile.InteractionDirection direction, Vector2Int tilePosition, StageTileInstance tileInstance, out bool playBumpSound) {
        if (RelocateTo == tilePosition) {
            // Don't redirect to the same tile.
            goto fail;
        }
        
        VersusStageData stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
        StageTileInstance nextTile = stage.GetTileRelative(f, RelocateTo);
        
        if (f.TryFindAsset(nextTile.Tile, out StageTile tile) && tile is IInteractableTile interactable) {
            return interactable.Interact(f, entity, direction, RelocateTo, nextTile, out playBumpSound);
        }

        fail:
        playBumpSound = false;
        return true;
    }
}