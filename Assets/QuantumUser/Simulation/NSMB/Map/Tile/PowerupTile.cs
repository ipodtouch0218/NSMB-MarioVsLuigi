using static IInteractableTile;
using Quantum;
using UnityEngine;

public unsafe class PowerupTile : BreakableBrickTile {

    //---Serialized Variables
    [SerializeField] private StageTileInstance resultTile;
    [SerializeField] private PowerupAsset smallPowerup, largePowerup;

    public override bool Interact(Frame f, EntityRef entity, InteractionDirection direction, Vector2Int tilePosition, StageTileInstance tileInstance, out bool playBumpSound) {
        if (base.Interact(f, entity, direction, tilePosition, tileInstance, out playBumpSound)) {
            return true;
        }

        if (!f.Unsafe.TryGetPointer(entity, out MarioPlayer* mario) && f.TryGet(entity, out Koopa koopa) && f.TryGet(entity, out Holdable holdable)) {
            if (koopa.IsKicked && holdable.PreviousHolder.IsValid) {
                f.Unsafe.TryGetPointer(holdable.PreviousHolder, out mario);
            }
        }

        if (mario == null) {
            playBumpSound = true;
            return false;
        }

        Bump(f, null, tilePosition, resultTile, direction == InteractionDirection.Down, 
            (mario->CurrentPowerupState < PowerupState.Mushroom ? smallPowerup : largePowerup).Prefab);
        playBumpSound = false;
        return false;
    }
}
