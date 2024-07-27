using static IInteractableTile;
using Quantum;
using UnityEngine;
using Photon.Deterministic;

public unsafe class CoinTile : BreakableBrickTile {

    //---Serialized Variables
    [SerializeField] private StageTileInstance resultTile;

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
            return false;
        }

        // Give coin to player
        f.Signals.MarioPlayerCollectedCoin(entity, mario, QuantumUtils.RelativeTileToWorld(f, new FPVector2(tilePosition.x, tilePosition.y)) + FPVector2.One * FP._0_25, true, direction == InteractionDirection.Down);
        Bump(f, null, tilePosition, resultTile, direction == InteractionDirection.Down);
        playBumpSound = false;

        return false;
    }
}