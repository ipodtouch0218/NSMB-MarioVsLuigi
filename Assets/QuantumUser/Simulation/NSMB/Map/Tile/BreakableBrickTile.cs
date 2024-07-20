using Quantum;
using System;
using UnityEngine;

public unsafe class BreakableBrickTile : StageTile, IInteractableTile {

    public Color ParticleColor;
    public BreakableBy BreakingRules = BreakableBy.SmallMarioDrill | BreakableBy.LargeMario | BreakableBy.LargeMarioGroundpound | BreakableBy.LargeMarioGroundpound | BreakableBy.MegaMario | BreakableBy.Shells | BreakableBy.Bombs;
    public bool BumpIfNotBroken;

    // [SerializeField] private Vector2Int tileSize = Vector2Int.one;

    public bool Interact(Frame f, EntityRef entity, IInteractableTile.InteractionDirection direction, Vector2Int tilePosition, StageTileInstance tileInstance) {
        bool doBreak = false;

        if (f.TryGet(entity, out MarioPlayer mario)) {
            // Mario interacting with the block
            if (mario.CurrentPowerupState < PowerupState.Mushroom) {
                doBreak = direction switch {
                    // Small Mario
                    IInteractableTile.InteractionDirection.Down when mario.IsGroundpoundActive => BreakingRules.HasFlag(BreakableBy.SmallMarioGroundpound),
                    IInteractableTile.InteractionDirection.Down when mario.IsDrilling => BreakingRules.HasFlag(BreakableBy.SmallMarioDrill),
                    IInteractableTile.InteractionDirection.Up => BreakingRules.HasFlag(BreakableBy.SmallMario),
                    _ => false
                };
            } else if (mario.CurrentPowerupState == PowerupState.MegaMushroom) {
                // Mega Mario
                doBreak = BreakingRules.HasFlag(BreakableBy.MegaMario);
            } else {
                doBreak = direction switch {
                    // Large Mario
                    IInteractableTile.InteractionDirection.Down when mario.IsGroundpoundActive => BreakingRules.HasFlag(BreakableBy.LargeMarioGroundpound),
                    IInteractableTile.InteractionDirection.Down when mario.IsDrilling => BreakingRules.HasFlag(BreakableBy.LargeMarioDrill),
                    IInteractableTile.InteractionDirection.Up => BreakingRules.HasFlag(BreakableBy.LargeMario),
                    _ => false
                };
            }
        } /*else if (f.TryGet(entity, out Koopa koopa)) {

             doBreak = breakableByShells;
             doBump = true;

        } else if (f.TryGet(entity, out Bobomb bobomb)) {


             doBump = false;
             doBreak = breakableByBombs;
        }*/

        if (doBreak) {
            // Break(interacter, worldLocation, giantBreak ? Enums.Sounds.Powerup_MegaMushroom_Break_Block : Enums.Sounds.World_Block_Break);
            // Bump(interacter, direction, worldLocation);
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            f.Events.TileBroken(f, entity, tilePosition.x, tilePosition.y, tileInstance);
            stage.SetTile(f, tilePosition.x, tilePosition.y, default);
        } else if (BumpIfNotBroken) {
            // BumpWithAnimation(interacter, direction, worldLocation);
        }

        return doBreak;
    }

    [Flags]
    public enum BreakableBy {
        SmallMario = 0,
        SmallMarioGroundpound = 1 << 0,
        SmallMarioDrill = 1 << 1,
        LargeMario = 1 << 2,
        LargeMarioGroundpound = 1 << 3,
        LargeMarioDrill = 1 << 4,
        MegaMario = 1 << 5,
        Shells = 1 << 6,
        Bombs = 1 << 7,
    }
}