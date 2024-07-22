using static IInteractableTile;
using Quantum;
using UnityEngine;
using Photon.Deterministic;

public unsafe class CoinTile : BreakableBrickTile {

    //---Serialized Variables
    [SerializeField] private StageTileInstance resultTile;

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

        // Give coin to player
        f.Signals.MarioPlayerCollectedCoin(entity, mario, new FPVector2(tilePosition.x + FP._0_25, tilePosition.y + FP._0_25));
        Bump(f, null, tilePosition, resultTile, direction == InteractionDirection.Down);

        return false;
    }
}