using static IInteractableTile;
using Quantum;
using System;
using UnityEngine;
using Photon.Deterministic;

public unsafe class BreakableBrickTile : StageTile, IInteractableTile {

    public Color ParticleColor;
    public BreakableBy BreakingRules = BreakableBy.SmallMarioDrill | BreakableBy.LargeMario | BreakableBy.LargeMarioGroundpound | BreakableBy.LargeMarioDrill | BreakableBy.MegaMario | BreakableBy.Shells | BreakableBy.Bombs;
    public bool BumpIfNotBroken;

    // [SerializeField] private Vector2Int tileSize = Vector2Int.one;

    public virtual bool Interact(Frame f, EntityRef entity, InteractionDirection direction, Vector2Int tilePosition, StageTileInstance tileInstance, out bool playBumpSound) {
        bool doBreak = false;
        bool doBump = true;
        playBumpSound = false;

        EntityRef bumpOwner = default;
        if (f.TryGet(entity, out MarioPlayer mario)) {
            // Mario interacting with the block
            if (mario.CurrentPowerupState < PowerupState.Mushroom) {
                doBreak = direction switch {
                    // Small Mario
                    InteractionDirection.Down when mario.IsGroundpoundActive => BreakingRules.HasFlag(BreakableBy.SmallMarioGroundpound),
                    InteractionDirection.Down when mario.IsDrilling => BreakingRules.HasFlag(BreakableBy.SmallMarioDrill),
                    InteractionDirection.Up => BreakingRules.HasFlag(BreakableBy.SmallMario),
                    _ => false
                };
            } else if (mario.CurrentPowerupState == PowerupState.MegaMushroom) {
                // Mega Mario
                doBreak = BreakingRules.HasFlag(BreakableBy.MegaMario);
            } else if (mario.IsInShell) {
                // Blue Shell
                doBreak = BreakingRules.HasFlag(BreakableBy.Shells);
            } else {
                doBreak = direction switch {
                    // Large Mario
                    InteractionDirection.Down when mario.IsGroundpoundActive => BreakingRules.HasFlag(BreakableBy.LargeMarioGroundpound),
                    InteractionDirection.Down when mario.IsDrilling => BreakingRules.HasFlag(BreakableBy.LargeMarioDrill),
                    InteractionDirection.Up => BreakingRules.HasFlag(BreakableBy.LargeMario),
                    _ => false
                };
            }
            bumpOwner = entity;
        } else if (f.TryGet(entity, out Koopa koopa) && koopa.IsKicked) {
            doBreak = BreakingRules.HasFlag(BreakableBy.Shells);
            doBump = true;
            bumpOwner = f.Get<Holdable>(entity).PreviousHolder;

        } /*else if (f.TryGet(entity, out Bobomb bobomb)) {
             doBreak = breakableByBombs;
             doBump = false;
        }*/

        var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
        if (doBreak) {
            f.Events.TileBroken(f, entity, tilePosition.x, tilePosition.y, tileInstance);
            stage.SetTileRelative(f, tilePosition.x, tilePosition.y, default);
        } else if (BumpIfNotBroken && doBump) {
            Bump(f, stage, tilePosition, tileInstance, direction == InteractionDirection.Down, bumpOwner);
        } else {
            playBumpSound = true;
        }

        return doBreak;
    }

    public void Bump(Frame f, VersusStageData stage, Vector2Int tile, StageTileInstance result, bool downwards, EntityRef owner, AssetRef<EntityPrototype> powerup = default) {
        stage = stage ? stage : f.FindAsset<VersusStageData>(f.Map.UserAsset);
        EntityRef newEntity = f.Create(f.SimulationConfig.BlockBumpPrototype);
        var blockBump = f.Unsafe.GetPointer<BlockBump>(newEntity);
        var transform = f.Unsafe.GetPointer<Transform2D>(newEntity);

        transform->Position = QuantumUtils.RelativeTileToWorld(f, new FPVector2(tile.x, tile.y)) + FPVector2.One * FP._0_25;
        blockBump->Origin = transform->Position;
        blockBump->StartTile = stage.GetTileRelative(f, tile.x, tile.y).Tile;
        blockBump->ResultTile = result;
        blockBump->Powerup = powerup;
        blockBump->IsDownwards = downwards;
        blockBump->TileX = tile.x;
        blockBump->TileY = tile.y;
        blockBump->Owner = owner;

        stage.SetTileRelative(f, tile.x, tile.y, new StageTileInstance {
            Tile = f.SimulationConfig.InvisibleSolidTile,
            Rotation = 0,
            Scale = FPVector2.One,
        });
    }

    [Flags]
    public enum BreakableBy {
        SmallMario = 1 << 0,
        SmallMarioGroundpound = 1 << 1,
        SmallMarioDrill = 1 << 2,
        LargeMario = 1 << 3,
        LargeMarioGroundpound = 1 << 4,
        LargeMarioDrill = 1 << 5,
        MegaMario = 1 << 6,
        Shells = 1 << 7,
        Bombs = 1 << 8,
    }
}