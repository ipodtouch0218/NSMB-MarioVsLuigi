using static IInteractableTile;
using Photon.Deterministic;
using Quantum;
using System;

public unsafe class BreakableBrickTile : StageTile, IInteractableTile {

#if QUANTUM_UNITY
    public UnityEngine.Color ParticleColor;
#endif
    public BreakableBy BreakingRules = BreakableBy.SmallMarioDrill | BreakableBy.LargeMario | BreakableBy.LargeMarioGroundpound | BreakableBy.LargeMarioDrill | BreakableBy.MegaMario | BreakableBy.Shells | BreakableBy.Bombs;
    public bool BumpIfNotBroken;
    public FPVector2 BumpSize = new FPVector2(FP._0_50, FP._0_50);
    public FPVector2 BumpOffset = FPVector2.Zero;

    // [SerializeField] private Vector2Int tileSize = Vector2Int.one;

    public virtual bool Interact(Frame f, EntityRef entity, InteractionDirection direction, IntVector2 tilePosition, StageTileInstance tileInstance, out bool playBumpSound) {
        bool doBreak = false;
        bool doBump = true;
        bool allowSelfDamage = false;
        playBumpSound = false;

        bool brokenByMega = false;
        EntityRef bumpOwner = default;
        if (f.Unsafe.TryGetPointer(entity, out MarioPlayer* mario)) {
            // Mario interacting with the block
            if (mario->CurrentPowerupState < PowerupState.Mushroom) {
                doBreak = direction switch {
                    // Small Mario
                    InteractionDirection.Down when mario->IsGroundpoundActive => BreakingRules.HasFlag(BreakableBy.SmallMarioGroundpound),
                    InteractionDirection.Down when mario->IsDrilling => BreakingRules.HasFlag(BreakableBy.SmallMarioDrill),
                    InteractionDirection.Up => BreakingRules.HasFlag(BreakableBy.SmallMario),
                    _ => false
                };
            } else if (mario->CurrentPowerupState == PowerupState.MegaMushroom) {
                // Mega Mario
                doBreak = BreakingRules.HasFlag(BreakableBy.MegaMario);
                brokenByMega = true;
            } else if (mario->IsInShell) {
                // Blue Shell
                doBreak = BreakingRules.HasFlag(BreakableBy.Shells);
            } else {
                doBreak = direction switch {
                    // Large Mario
                    InteractionDirection.Down when mario->IsGroundpoundActive => BreakingRules.HasFlag(BreakableBy.LargeMarioGroundpound),
                    InteractionDirection.Down when mario->IsDrilling => BreakingRules.HasFlag(BreakableBy.LargeMarioDrill),
                    InteractionDirection.Up => BreakingRules.HasFlag(BreakableBy.LargeMario),
                    _ => false
                };
            }
            bumpOwner = entity;
        } else if (f.Unsafe.TryGetPointer(entity, out Koopa* koopa) && koopa->IsKicked) {
            doBreak = BreakingRules.HasFlag(BreakableBy.Shells);
            doBump = true;
            bumpOwner = f.Unsafe.GetPointer<Holdable>(entity)->PreviousHolder;
            allowSelfDamage = true;
            
        } else if (f.Has<Bobomb>(entity)) {
             doBreak = BreakingRules.HasFlag(BreakableBy.Bombs);
             doBump = false;
        }

        bool fromBelow = direction != InteractionDirection.Down;
        var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
        if (doBreak) {
            BlockBumpSystem.Bump(f, QuantumUtils.RelativeTileToWorldRounded(stage, tilePosition), bumpOwner, allowSelfDamage, fromBelow);
            f.Events.TileBroken(entity, tilePosition, tileInstance, brokenByMega);
            stage.SetTileRelative(f, tilePosition, default);

        } else if (BumpIfNotBroken && doBump) {
            Bump(f, stage, tilePosition, tileInstance, !fromBelow, bumpOwner, allowSelfDamage);
        } else {
            playBumpSound = true;
        }

        return doBreak;
    }

    public static void Bump(Frame f, VersusStageData stage, IntVector2 tilePosition, StageTileInstance result, bool downwards, EntityRef owner, bool allowSelfDamage, AssetRef<EntityPrototype> powerup = default) {
        if (stage == null) {
            stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
        }
        Bump(f, stage, tilePosition, stage.GetTileRelative(f, tilePosition).Tile, result, downwards, owner, allowSelfDamage, powerup);
    }

    public static void Bump(Frame f, VersusStageData stage, IntVector2 tilePosition, AssetRef<StageTile> tile, StageTileInstance result, bool downwards, EntityRef owner, bool allowSelfDamage, AssetRef<EntityPrototype> powerup = default) {
        if (stage == null) {
            stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
        }
        EntityRef newEntity = f.Create(f.SimulationConfig.BlockBumpPrototype);
        var blockBump = f.Unsafe.GetPointer<BlockBump>(newEntity);
        var transform = f.Unsafe.GetPointer<Transform2D>(newEntity);

        transform->Position = QuantumUtils.RelativeTileToWorld(f, tilePosition) + FPVector2.One * FP._0_25;
        blockBump->Origin = transform->Position;
        blockBump->StartTile = tile;
        blockBump->ResultTile = result;
        blockBump->Powerup = powerup;
        blockBump->IsDownwards = downwards;
        blockBump->Tile = tilePosition;
        blockBump->Owner = owner;
        blockBump->AllowSelfDamage = allowSelfDamage;

        stage.SetTileRelative(f, tilePosition, new StageTileInstance {
            Tile = f.SimulationConfig.InvisibleSolidTile,
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