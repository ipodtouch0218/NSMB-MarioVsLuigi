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

        if (!f.Unsafe.TryGetPointer(entity, out MarioPlayer* mario)
            && f.Unsafe.TryGetPointer(entity, out Koopa* koopa)
            && koopa->IsKicked
            && f.Unsafe.TryGetPointer(entity, out Holdable* holdable)
            && f.Exists(holdable->PreviousHolder)) {

            // Talk to my dad, his name is mario :)
            f.Unsafe.TryGetPointer(holdable->PreviousHolder, out mario);
            entity = holdable->PreviousHolder;
        }

        if (mario == null) {
            playBumpSound = true;
            return false;
        }

        Bump(f, null, tilePosition, resultTile, direction == InteractionDirection.Down, entity,
            (mario->CurrentPowerupState < PowerupState.Mushroom ? smallPowerup : largePowerup).Prefab);
        playBumpSound = false;
        return false;
    }
}
