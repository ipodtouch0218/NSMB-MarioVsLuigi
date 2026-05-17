using System;
using Photon.Deterministic;
using Quantum;

public unsafe class CoinItemAsset : AssetObject {

    public AssetRef<EntityPrototype> Prefab;
    public FP SpawnChance = FP._0_10, AboveAverageBonus = 0, BelowAverageBonus = 0;
    public SoundEffect BlockSpawnSoundEffect = SoundEffect.World_Block_Powerup;
    public TypeFlags Flags = TypeFlags.SpawnableFromCoins | TypeFlags.SpawnableFromRouletteBlock | TypeFlags.LaunchableFromBlock;
    public int MaxNumberOfItems = 0;
    public FP CanSpawnAfterSeconds = 0;

#if QUANTUM_UNITY
    public string TranslationKey;
#endif

    public FPVector2 CameraSpawnOffset = new(0, FP.FromString("1.68"));

    public virtual int CountItemsExisting(Frame f) {
        int numOfItems = 0;
        foreach ((_, var coinItem) in f.Unsafe.GetComponentBlockIterator<CoinItem>()) {
            if (coinItem->Scriptable == this) {
                numOfItems++;
            }
        }
        return numOfItems;
    }

    public virtual void InitializeFromBlockBump(Frame f, EntityRef entity, ref BlockBumpSystem.Filter blockBumpFilter) {
        var blockBump = blockBumpFilter.BlockBump;
        var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);

        // Launch if downwards bump and theres a (solid) block below us
        BreakableBrickTile tile = (BreakableBrickTile) f.FindAsset(blockBump->StartTile);
        bool launch = false;
        if (Flags.HasFlag(TypeFlags.LaunchableFromBlock)) {
            if (blockBump->IsDownwards) {
                // Downwards check
                IntVector2 below = blockBump->Tile;
                below.Y -= FPMath.RoundToInt(tile.BumpSize.Y * 2);
                StageTileInstance belowTileInstance = stage.GetTileRelative(f, below);

                if (belowTileInstance.HasWorldPolygons(f)) {
                    // Launch spawn
                    launch = true;
                }
            } else {
                // Upwards check
                IntVector2 above = blockBump->Tile;
                above.Y += FPMath.RoundToInt(tile.BumpSize.Y * 2);
                StageTileInstance aboveTileInstance = stage.GetTileRelative(f, above);

                if (aboveTileInstance.HasWorldPolygons(f)) {
                    // Launch spawn
                    launch = true;
                }
            }
        }

        var coinItem = f.Unsafe.GetPointer<CoinItem>(entity);
        FPVector2 origin = blockBumpFilter.Transform->Position;
        if (launch) {
            // Launch to right by default- check for block to the right
            bool launchToRight = true;

            IntVector2 right = blockBump->Tile;
            right.X += FPMath.RoundToInt(tile.BumpSize.X * 2);
            StageTileInstance rightTileInstance = stage.GetTileRelative(f, right);

            if (rightTileInstance.HasWorldPolygons(f)) {
                // Check to the left
                IntVector2 left = blockBump->Tile;
                left.X -= FPMath.RoundToInt(tile.BumpSize.X * 2);
                StageTileInstance leftTileInstance = stage.GetTileRelative(f, left);

                launchToRight = leftTileInstance.HasWorldPolygons(f);
            }

            coinItem->InitializeLaunchSpawn(f, entity, launchToRight, origin);
            coinItem->IgnorePlayerFrames = 20;
        } else {
            // Normal block spawn
            if (blockBump->IsDownwards) {
                origin.Y -= (tile.BumpSize.Y / 2);
            } else {
                origin.Y += (tile.BumpSize.Y / 2) - FP._0_50;
            }

            coinItem->InitializeBlockSpawn(f, entity, 60,
                origin,
                origin + (blockBump->IsDownwards ? new FPVector2(0, -FP._0_50) : new FPVector2(0, FP._0_50)));
            coinItem->IgnorePlayerFrames = 5;
        }
    }

    public virtual bool CanSpawn(Frame f, bool fromRouletteBlock) {
        if (!fromRouletteBlock && !Flags.HasFlag(TypeFlags.SpawnableFromCoins)) {
            return false;
        }
        if (fromRouletteBlock && !Flags.HasFlag(TypeFlags.SpawnableFromRouletteBlock)) {
            return false;
        }
        if (Flags.HasFlag(TypeFlags.NonVanillaItem) && !f.Global->Rules.CustomPowerupsEnabled) {
            return false;
        }
        if (Flags.HasFlag(TypeFlags.LivesEnabledOnly) && !f.Global->Rules.IsLivesEnabled) {
            return false;
        }
        FP secondsSinceStart = (FP) (f.Number - f.Global->StartFrame) * f.DeltaTime;
        if (secondsSinceStart < CanSpawnAfterSeconds) {
            return false;
        }
        if (MaxNumberOfItems > 0 && CountItemsExisting(f) >= MaxNumberOfItems) {
            return false;
        }
        return true;
    }

    [Flags]
    public enum TypeFlags : byte {
        None = 0,
        SpawnableFromCoins = 1 << 0,
        SpawnableFromRouletteBlock = 1 << 1,
        NonVanillaItem = 1 << 2,
        LivesEnabledOnly = 1 << 3,
        LaunchableFromBlock = 1 << 4,
    }
}