using Photon.Deterministic;
using Quantum;
using Quantum.Collections;
using Quantum.Core;
using System;
using System.Collections.Generic;

public static unsafe class QuantumUtils {

    private static readonly SoundEffect[] ComboSounds = {
        SoundEffect.Enemy_Shell_Kick,
        SoundEffect.Enemy_Shell_Combo1,
        SoundEffect.Enemy_Shell_Combo2,
        SoundEffect.Enemy_Shell_Combo3,
        SoundEffect.Enemy_Shell_Combo4,
        SoundEffect.Enemy_Shell_Combo5,
        SoundEffect.Enemy_Shell_Combo6,
        SoundEffect.Enemy_Shell_Combo7,
    };

    public static unsafe PlayerData* GetPlayerData(Frame f, PlayerRef player, QDictionary<PlayerRef, EntityRef>? dictionary = default) {

        QDictionary<PlayerRef, EntityRef> playerDataDictionary; 
        if (dictionary == null) {
            if (!f.TryResolveDictionary(f.Global->PlayerDatas, out playerDataDictionary)) {
                return null;
            }
        } else {
            playerDataDictionary = dictionary.Value;
        }

        if (!playerDataDictionary.TryGetValue(player, out EntityRef playerDataEntity)
            || !f.Unsafe.TryGetPointer(playerDataEntity, out PlayerData* data)) {

            return null;
        }

        return data;
    }

    public static PlayerData? GetPlayerDataSafe(Frame f, PlayerRef player, QDictionary<PlayerRef, EntityRef>? dictionary = default) {

        QDictionary<PlayerRef, EntityRef> playerDataDictionary;
        if (dictionary == null) {
            if (!f.TryResolveDictionary(f.Global->PlayerDatas, out playerDataDictionary)) {
                return null;
            }
        } else {
            playerDataDictionary = dictionary.Value;
        }

        if (!playerDataDictionary.TryGetValue(player, out EntityRef playerDataEntity)
            || !f.TryGet(playerDataEntity, out PlayerData data)) {

            return null;
        }

        return data;
    }

    public static SoundEffect GetComboSoundEffect(int combo) {
        return ComboSounds[FPMath.Clamp(combo, 0, ComboSounds.Length - 1)];
    }

    public static IntVector2 WorldToUnityTile(Frame f, FPVector2 worldPos) {
        return WorldToUnityTile(f.FindAsset<VersusStageData>(f.Map.UserAsset), worldPos);
    }

    public static IntVector2 WorldToUnityTile(VersusStageData stage, FPVector2 worldPos) {
        worldPos -= stage.TilemapWorldPosition;
        worldPos *= 2;
        return new IntVector2(FPMath.FloorToInt(worldPos.X), FPMath.FloorToInt(worldPos.Y));
    }

    public static IntVector2 UntiyTileToRelativeTile(Frame f, IntVector2 unityTile) {
        return UnityTileToRelativeTile(f.FindAsset<VersusStageData>(f.Map.UserAsset), unityTile);
    }

    public static IntVector2 UnityTileToRelativeTile(VersusStageData stage, IntVector2 unityTile, bool extend = true) {
        int x = unityTile.X - stage.TileOrigin.X;
        x = (x % stage.TileDimensions.X + stage.TileDimensions.X) % stage.TileDimensions.X; // Wrapping
        int y = unityTile.Y - stage.TileOrigin.Y;
        if (extend && stage.ExtendCeilingHitboxes) {
            y = Math.Min(y, stage.TileDimensions.Y - 1);
        }
        return new IntVector2(x, y);
    }

    public static IntVector2 WorldToRelativeTile(Frame f, FPVector2 worldPos, bool extend = true) {
        return WorldToRelativeTile(f.FindAsset<VersusStageData>(f.Map.UserAsset), worldPos, extend);
    }

    public static IntVector2 WorldToRelativeTile(VersusStageData stage, FPVector2 worldPos, bool extend = true) {
        return UnityTileToRelativeTile(stage, WorldToUnityTile(stage, worldPos), extend);
    }

    public static FPVector2 UnityTileToWorld(Frame f, IntVector2 unityTile) {
        return UnityTileToWorld(f.FindAsset<VersusStageData>(f.Map.UserAsset), unityTile);
    }

    public static FPVector2 UnityTileToWorld(VersusStageData stage, IntVector2 unityTile) {
        return (new FPVector2(unityTile.X, unityTile.Y) / 2) + stage.TilemapWorldPosition;
    }

    public static IntVector2 RelativeTileToUnityTile(Frame f, IntVector2 relativeTile) {
        return RelativeTileToUnityTile(f.FindAsset<VersusStageData>(f.Map.UserAsset), relativeTile);
    }

    public static IntVector2 RelativeTileToUnityTile(VersusStageData stage, IntVector2 relativeTile) {
        int x = relativeTile.X + stage.TileOrigin.X;
        int y = relativeTile.Y + stage.TileOrigin.Y;
        return new IntVector2(x, y);
    }

    public static FPVector2 RelativeTileToWorld(Frame f, IntVector2 relativeTile) {
        return RelativeTileToWorld(f.FindAsset<VersusStageData>(f.Map.UserAsset), relativeTile);
    }

    public static FPVector2 RelativeTileToWorld(VersusStageData stage, IntVector2 relativeTile) {
        return UnityTileToWorld(stage, RelativeTileToUnityTile(stage, relativeTile));
    }

    public static FPVector2 RelativeTileToWorldRounded(VersusStageData stage, IntVector2 relativeTile) {
        return RelativeTileToWorld(stage, relativeTile) + FPVector2.One * FP._0_25;
    }

    public static IntVector2 WrapRelativeTile(VersusStageData stage, IntVector2 relativeTile, out WrapDirection wrapDirection) {
        if (relativeTile.X < 0) {
            relativeTile.X += stage.TileDimensions.X;
            wrapDirection = WrapDirection.Left;

        } else if (relativeTile.X >= stage.TileDimensions.X) {
            relativeTile.X -= stage.TileDimensions.X;
            wrapDirection = WrapDirection.Right;

        } else {
            wrapDirection = WrapDirection.NoWrap;
        }

        return relativeTile;
    }

    public static FPVector2 WrapUnityTile(Frame f, FPVector2 unityTile, out WrapDirection wrapDirection) {
        return WrapUnityTile(f.FindAsset<VersusStageData>(f.Map.UserAsset), unityTile, out wrapDirection);
    }

    public static FPVector2 WrapUnityTile(VersusStageData stage, FPVector2 unityTile, out WrapDirection wrapDirection) {
        if (unityTile.X < stage.TileOrigin.X) {
            unityTile.X += stage.TileDimensions.X;
            wrapDirection = WrapDirection.Left;

        } else if (unityTile.X >= stage.TileOrigin.X + stage.TileDimensions.X) {
            unityTile.X -= stage.TileDimensions.X;
            wrapDirection = WrapDirection.Right;

        } else {
            wrapDirection = WrapDirection.NoWrap;
        }

        return unityTile;
    }

    public static FPVector2 WrapWorld(FrameBase f, FPVector2 worldPos, out WrapDirection wrapDirection) {
        return WrapWorld(f.FindAsset<VersusStageData>(f.Map.UserAsset), worldPos, out wrapDirection);
    }

    public static FPVector2 WrapWorld(VersusStageData stage, FPVector2 worldPos, out WrapDirection wrapDirection) {
        if (worldPos.X < stage.StageWorldMin.X) {
            worldPos.X += stage.TileDimensions.X / 2;
            wrapDirection = WrapDirection.Left;

        } else if (worldPos.X >= stage.StageWorldMax.X) {
            worldPos.X -= stage.TileDimensions.X / 2;
            wrapDirection = WrapDirection.Right;

        } else {
            wrapDirection = WrapDirection.NoWrap;
        }

        return worldPos;
    }

    public static void UnwrapWorldLocations(Frame f, FPVector2 a, FPVector2 b, out FPVector2 newA, out FPVector2 newB) {
        UnwrapWorldLocations(f.FindAsset<VersusStageData>(f.Map.UserAsset), a, b, out newA, out newB);
    }

    public static void UnwrapWorldLocations(VersusStageData stage, FPVector2 a, FPVector2 b, out FPVector2 newA, out FPVector2 newB) {
        newA = a;
        newB = b;

        if (!stage.IsWrappingLevel) {
            return;
        }

        FP width = stage.TileDimensions.X * FP._0_50;
        if (FPMath.Abs(newA.X - newB.X) > width / 2) {
            newB.X += width * (newB.X > stage.StageWorldMin.X + (width / 2) ? -1 : 1);
        }
    }

    public enum WrapDirection {
        NoWrap,
        Left,
        Right
    }

    public static int GetValidTeams(Frame f) {
        int result = 0;

        var allPlayers = f.Filter<PlayerData>();
        while (allPlayers.NextUnsafe(out _, out PlayerData* data)) {
            if (data->IsSpectator) {
                continue;
            }

            byte team = data->RealTeam;
            result |= (1 << team);
        }

        return result;
    }

    public static FP WrappedDistance(Frame f, FPVector2 a, FPVector2 b) {
        return WrappedDistance(f, a, b, out _);
    }

    public static FP WrappedDistance(VersusStageData stage, FPVector2 a, FPVector2 b) {
        return WrappedDistance(stage, a, b, out _);
    }

    public static FP WrappedDistance(Frame f, FPVector2 a, FPVector2 b, out FP xDifference) {
        return WrappedDistance(f.FindAsset<VersusStageData>(f.Map.UserAsset), a, b, out xDifference);
    }

    public static FP WrappedDistance(VersusStageData stage, FPVector2 a, FPVector2 b, out FP xDifference) {
        FP width = stage.TileDimensions.X * FP._0_50;
        if (stage.IsWrappingLevel && FPMath.Abs(a.X - b.X) > width * FP._0_50) {
            a.X -= width * FPMath.Sign(a.X - b.X);
        }

        xDifference = a.X - b.X;
        return FPVector2.Distance(a, b);
    }

    public static FP WrappedDistanceSquared(VersusStageData stage, FPVector2 a, FPVector2 b) {
        FP width = stage.TileDimensions.X * FP._0_50;
        if (stage.IsWrappingLevel && FPMath.Abs(a.X - b.X) > width * FP._0_50) {
            a.X -= width * FPMath.Sign(a.X - b.X);
        }

        return FPVector2.DistanceSquared(a, b);
    }

    public static FP EaseInOut(FP x) {
        return x < FP._0_50 ? 2 * x * x : 1 - ((-2 * x + 2) * (-2 * x + 2) / 2);
    }

    public static FP EaseIn(FP x) {
        return x * x;
    }

    public static FP EaseOut(FP x) {
        return 1 - (1 - x) * (1 - x);
    }

    public static FP SmoothDamp(FP current, FP target, ref FP currentVelocity, FP smoothTime, FP maxSpeed, FP deltaTime) {
        smoothTime = FPMath.Max(Constants._0_0001, smoothTime);
        FP num = 2 / smoothTime;
        FP num2 = num * deltaTime;
        FP num3 = 1 / (1 + num2 + Constants._0_48 * num2 * num2 + Constants._0_235 * num2 * num2 * num2);
        FP value = current - target;
        FP num4 = target;
        FP num5 = maxSpeed * smoothTime;
        value = FPMath.Clamp(value, 0 - num5, num5);
        target = current - value;
        FP num6 = (currentVelocity + num * value) * deltaTime;
        currentVelocity = (currentVelocity - num * num6) * num3;
        FP num7 = target + (value + num6) * num3;
        if (num4 - current > 0 == num7 > num4) {
            num7 = num4;
            currentVelocity = (num7 - num4) / deltaTime;
        }

        return num7;
    }

    public static FPVector2 SmoothDamp(FPVector2 current, FPVector2 target, ref FPVector2 currentVelocity, FP smoothTime, FP maxSpeed, FP deltaTime) {
        smoothTime = FPMath.Max(Constants._0_0001, smoothTime);
        FP num = 2 / smoothTime;
        FP num2 = num * deltaTime;
        FP num3 = (FP) 1 / (1 + num2 + Constants._0_48 * num2 * num2 + Constants._0_235 * num2 * num2 * num2);
        FP num4 = current.X - target.X;
        FP num5 = current.Y - target.Y;
        FPVector2 vector = target;
        FP num6 = maxSpeed * smoothTime;
        FP num7 = num6 * num6;
        FP num8 = num4 * num4 + num5 * num5;
        if (num8 > num7) {
            FP num9 = FPMath.Sqrt(num8);
            num4 = num4 / num9 * num6;
            num5 = num5 / num9 * num6;
        }

        target.X = current.X - num4;
        target.Y = current.Y - num5;
        FP num10 = (currentVelocity.X + num * num4) * deltaTime;
        FP num11 = (currentVelocity.Y + num * num5) * deltaTime;
        currentVelocity.X = (currentVelocity.X - num * num10) * num3;
        currentVelocity.Y = (currentVelocity.Y - num * num11) * num3;
        FP num12 = target.X + (num4 + num10) * num3;
        FP num13 = target.Y + (num5 + num11) * num3;
        FP num14 = vector.X - current.X;
        FP num15 = vector.Y - current.Y;
        FP num16 = num12 - vector.X;
        FP num17 = num13 - vector.Y;
        if (num14 * num16 + num15 * num17 > 0) {
            num12 = vector.X;
            num13 = vector.Y;
            currentVelocity.X = (num12 - vector.X) / deltaTime;
            currentVelocity.Y = (num13 - vector.Y) / deltaTime;
        }

        return new FPVector2(num12, num13);
    }

    public static FP DeltaAngle(FP current, FP target) {
        FP num = FPMath.Repeat(target - current, 360);
        if (num > 180) {
            num -= 360;
        }

        return num;
    }

    public static FP MoveTowards(FP current, FP target, FP maxDelta) {
        if (FPMath.Abs(target - current) <= maxDelta) {
            return target;
        }

        return current + FPMath.Sign(target - current) * maxDelta;
    }

    public static FP MoveTowardsAngle(FP current, FP target, FP maxDelta) {
        FP num = DeltaAngle(current, target);
        if (0 - maxDelta < num && num < maxDelta) {
            return target;
        }

        target = current + num;
        return MoveTowards(current, target, maxDelta);
    }

    public static int WrappedDirectionSign(Frame f, FPVector2 a, FPVector2 b) {
        return WrappedDirectionSign(f.FindAsset<VersusStageData>(f.Map.UserAsset), a, b);
    }

    public static int WrappedDirectionSign(VersusStageData stage, FPVector2 a, FPVector2 b) {
        if (!stage.IsWrappingLevel) {
            return a.X > b.X ? 1 : -1;
        }

        return (a.X > b.X ^ FPMath.Abs(a.X - b.X) > stage.TileDimensions.X * FP._0_25) ? 1 : -1;
    }

    public static FPVector2 WrappedLerp(Frame f, FPVector2 a, FPVector2 b, FP alpha) {
        return WrappedLerp(f.FindAsset<VersusStageData>(f.Map.UserAsset), a, b, alpha);
    }

    public static FPVector2 WrappedLerp(VersusStageData stage, FPVector2 a, FPVector2 b, FP alpha) {
        UnwrapWorldLocations(stage, a, b, out var newA, out var newB);
        FPVector2 lerped = FPVector2.Lerp(newA, newB, alpha);
        return WrapWorld(stage, lerped, out _);
    }

    public static PowerupAsset FindPowerupAsset(Frame f, PowerupState state) {
        var gamemode = f.FindAsset(f.Global->Rules.Gamemode);
        foreach (var coinItemAsset in gamemode.AllCoinItems) {
            if (f.TryFindAsset(coinItemAsset, out CoinItemAsset item)
                && item is PowerupAsset powerup
                && powerup.State == state) {

                return powerup;
            }
        }
        return null;
    }

    public static bool IsGameStartable(Frame f) {
        // If game is already started, it's valid.
        if (f.Global->GameState != GameState.PreGameRoom) {
            return true;
        }

        int playerDataCount = f.ComponentCount<PlayerData>();
        PlayerData** allPlayerDatas = stackalloc PlayerData*[playerDataCount];
        
        int index = 0;
        var playerDataFilter = f.Filter<PlayerData>();
        playerDataFilter.UseCulling = false;

        while (playerDataFilter.NextUnsafe(out _, out PlayerData* pd)) {
            allPlayerDatas[index++] = pd;
        }

        // Check that at least one non-spectator exists
        bool nonSpectator = false;
        for (int i = 0; i < playerDataCount; i++) {
            PlayerData* pd = allPlayerDatas[i];
            if (!pd->IsSpectator && !pd->ManualSpectator) {
                nonSpectator = true;
                break;
            }
        }
        if (!nonSpectator) {
            return false;
        }

        // Check that at least two teams exist
        if (f.Global->Rules.TeamsEnabled && playerDataCount > 1) {
            byte? firstTeam = null;
            for (int i = 0; i < playerDataCount; i++) {
                PlayerData* pd = allPlayerDatas[i];
                if (pd->IsSpectator || pd->ManualSpectator) {
                    continue;
                }

                byte team = pd->RequestedTeam;
                if (firstTeam.HasValue) {
                    if (firstTeam != team) {
                        goto skip;
                    }
                } else {
                    firstTeam = team;
                }
            }
            return false;
        }

        skip:
        return true;
    }

    public static bool Decrement(ref byte timer) {
        if (timer > 0) {
            return --timer == 0;
        }

        return true;
    }

    public static bool Decrement(ref ushort timer) {
        if (timer > 0) {
            return --timer == 0;
        }

        return true;
    }

    public static bool Decrement(ref int timer) {
        if (timer > 0) {
            return --timer == 0;
        }

        return true;
    }
}

public static class Extensions {
    public static IEnumerator<T> GetEnumerator<T>(this IEnumerator<T> enumerator) => enumerator;
}