using Photon.Deterministic;
using Quantum;
using UnityEngine;
using static IInteractableTile;

public unsafe class CoinTile : BreakableBrickTile {

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
            return false;
        }

        // Give coin to player
        f.Signals.OnMarioPlayerCollectedCoin(entity, mario, QuantumUtils.RelativeTileToWorld(f, tilePosition) + FPVector2.One * FP._0_25, true, direction == InteractionDirection.Down);
        Bump(f, null, tilePosition, resultTile, direction == InteractionDirection.Down, entity, allowSelfDamage);
        playBumpSound = false;

        return false;
    }
}