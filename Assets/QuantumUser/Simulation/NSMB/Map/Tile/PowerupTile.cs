using static IInteractableTile;
using Quantum;
using UnityEngine;

public unsafe class PowerupTile : BreakableBrickTile {

    //---Serialized Variables
    [SerializeField] private StageTileInstance resultTile;
    [SerializeField] private PowerupAsset smallPowerup, largePowerup;

    public override bool Interact(Frame f, EntityRef entity, InteractionDirection direction, Vector2Int tilePosition, StageTileInstance tileInstance) {
        if (base.Interact(f, entity, direction, tilePosition, tileInstance)) {
            return true;
        }

        if (!f.Unsafe.TryGetPointer(entity, out MarioPlayer* mario)/* || f.TryGet(entity, out Koopa koopa) */) {
            /*
            if (koopa.IsInShell && koopa.Owner.IsValid) {
                mario = f.Unsage.TryGetPointer(koopa.Owner, out mario);
            }
            */
        }

        if (mario == null) {
            return false;
        }

        Bump(f, null, tilePosition, resultTile, direction == InteractionDirection.Down, 
            (mario->CurrentPowerupState < PowerupState.Mushroom ? smallPowerup : largePowerup).Prefab);
        return false;
    }
}
