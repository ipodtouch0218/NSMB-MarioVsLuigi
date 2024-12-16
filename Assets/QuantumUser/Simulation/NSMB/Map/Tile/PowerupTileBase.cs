using Quantum;
using static IInteractableTile;
using UnityEngine;

public unsafe abstract class PowerupTileBase : BreakableBrickTile {

    //---Serialized Variables
    [SerializeField] private StageTileInstance resultTile;

    public override bool Interact(Frame f, EntityRef entity, InteractionDirection direction, Vector2Int tilePosition, StageTileInstance tileInstance, out bool playBumpSound) {
        if (base.Interact(f, entity, direction, tilePosition, tileInstance, out playBumpSound)) {
            return true;
        }

        bool allowSelfDamage = false;
        if (!f.Unsafe.TryGetPointer(entity, out MarioPlayer* mario)
            && f.Unsafe.TryGetPointer(entity, out Koopa* koopa)
            && koopa->IsKicked
            && f.Unsafe.TryGetPointer(entity, out Holdable* holdable)
            && f.Exists(holdable->PreviousHolder)) {

            // Talk to my dad, his name is mario :)
            f.Unsafe.TryGetPointer(holdable->PreviousHolder, out mario);
            entity = holdable->PreviousHolder;
            allowSelfDamage = true;
        }

        if (mario == null) {
            playBumpSound = true;
            return false;
        }

        Bump(f, null, tilePosition, resultTile, direction == InteractionDirection.Down, entity, allowSelfDamage, GetPowerupAsset(f, entity, mario).Prefab);
        playBumpSound = false;
        return false;
    }

    public abstract PowerupAsset GetPowerupAsset(Frame f, EntityRef marioEntity, MarioPlayer* mario);
}