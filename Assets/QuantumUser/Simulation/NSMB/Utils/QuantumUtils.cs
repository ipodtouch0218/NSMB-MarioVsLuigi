using Photon.Deterministic;
using Quantum;
using Quantum.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;

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

    public static unsafe PlayerRef GetHostPlayer(Frame f, out PlayerData* data) {
        var filter = f.Filter<PlayerData>();
        filter.UseCulling = false;
        while (filter.NextUnsafe(out _, out PlayerData* playerData)) {
            if (playerData->IsRoomHost) {
                data = playerData;
                return playerData->PlayerRef;
            }
        }

        data = null;
        return PlayerRef.None;
    }


    public static SoundEffect GetComboSoundEffect(int combo) {
        return ComboSounds[Mathf.Clamp(combo, 0, ComboSounds.Length - 1)];
    }

    public static Vector2Int WorldToUnityTile(Frame f, FPVector2 worldPos) {
        return WorldToUnityTile(f.FindAsset<VersusStageData>(f.Map.UserAsset), worldPos);
    }

    public static Vector2Int WorldToUnityTile(VersusStageData stage, FPVector2 worldPos) {
        worldPos -= stage.TilemapWorldPosition;
        worldPos *= 2;
        return new Vector2Int(FPMath.FloorToInt(worldPos.X), FPMath.FloorToInt(worldPos.Y));
    }

    public static Vector2Int UntiyTileToRelativeTile(Frame f, Vector2Int unityTile) {
        return UnityTileToRelativeTile(f.FindAsset<VersusStageData>(f.Map.UserAsset), unityTile);
    }

    public static Vector2Int UnityTileToRelativeTile(VersusStageData stage, Vector2Int unityTile) {
        int x = unityTile.x - stage.TileOrigin.x;
        x = (x % stage.TileDimensions.x + stage.TileDimensions.x) % stage.TileDimensions.x; // Wrapping
        int y = unityTile.y - stage.TileOrigin.y;
        if (stage.ExtendCeilingHitboxes) {
            y = Mathf.Min(y, stage.TileDimensions.y - 1);
        }
        return new Vector2Int(x, y);
    }

    public static Vector2Int WorldToRelativeTile(Frame f, FPVector2 worldPos) {
        return WorldToRelativeTile(f.FindAsset<VersusStageData>(f.Map.UserAsset), worldPos);
    }

    public static Vector2Int WorldToRelativeTile(VersusStageData stage, FPVector2 worldPos) {
        return UnityTileToRelativeTile(stage, WorldToUnityTile(stage, worldPos));
    }

    public static FPVector2 UnityTileToWorld(Frame f, Vector2Int unityTile) {
        return UnityTileToWorld(f.FindAsset<VersusStageData>(f.Map.UserAsset), unityTile);
    }

    public static FPVector2 UnityTileToWorld(VersusStageData stage, Vector2Int unityTile) {
        return (new FPVector2(unityTile.x, unityTile.y) / 2) + stage.TilemapWorldPosition;
    }

    public static Vector2Int RelativeTileToUnityTile(Frame f, Vector2Int relativeTile) {
        return RelativeTileToUnityTile(f.FindAsset<VersusStageData>(f.Map.UserAsset), relativeTile);
    }

    public static Vector2Int RelativeTileToUnityTile(VersusStageData stage, Vector2Int relativeTile) {
        int x = relativeTile.x + stage.TileOrigin.x;
        int y = relativeTile.y + stage.TileOrigin.y;
        return new Vector2Int(x, y);
    }

    public static FPVector2 RelativeTileToWorld(Frame f, Vector2Int relativeTile) {
        return RelativeTileToWorld(f.FindAsset<VersusStageData>(f.Map.UserAsset), relativeTile);
    }

    public static FPVector2 RelativeTileToWorld(VersusStageData stage, Vector2Int relativeTile) {
        return UnityTileToWorld(stage, RelativeTileToUnityTile(stage, relativeTile));
    }

    public static FPVector2 RelativeTileToWorldRounded(VersusStageData stage, Vector2Int relativeTile) {
        return RelativeTileToWorld(stage, relativeTile) + FPVector2.One * FP._0_25;
    }

    public static FPVector2 WrapUnityTile(Frame f, FPVector2 unityTile, out WrapDirection wrapDirection) {
        return WrapUnityTile(f.FindAsset<VersusStageData>(f.Map.UserAsset), unityTile, out wrapDirection);
    }

    public static FPVector2 WrapUnityTile(VersusStageData stage, FPVector2 unityTile, out WrapDirection wrapDirection) {
        if (unityTile.X < stage.TileOrigin.x) {
            unityTile.X += stage.TileDimensions.x;
            wrapDirection = WrapDirection.Left;

        } else if (unityTile.X >= stage.TileOrigin.x + stage.TileDimensions.x) {
            unityTile.X -= stage.TileDimensions.x;
            wrapDirection = WrapDirection.Right;

        } else {
            wrapDirection = WrapDirection.NoWrap;
        }

        return unityTile;
    }

    public static FPVector2 WrapWorld(Frame f, FPVector2 worldPos, out WrapDirection wrapDirection) {
        return WrapWorld(f.FindAsset<VersusStageData>(f.Map.UserAsset), worldPos, out wrapDirection);
    }

    public static FPVector2 WrapWorld(VersusStageData stage, FPVector2 worldPos, out WrapDirection wrapDirection) {
        if (worldPos.X < stage.StageWorldMin.X) {
            worldPos.X += stage.TileDimensions.x / 2;
            wrapDirection = WrapDirection.Left;

        } else if (worldPos.X >= stage.StageWorldMax.X) {
            worldPos.X -= stage.TileDimensions.x / 2;
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

        FP width = stage.TileDimensions.x * FP._0_50;
        if (FPMath.Abs(newA.X - newB.X) > width / 2) {
            newB.X += width * (newB.X > stage.StageWorldMin.X + (width / 2) ? -1 : 1);
        }
    }

    public enum WrapDirection {
        NoWrap,
        Left,
        Right
    }

    public static int? GetWinningTeam(Frame f, out int winningStars) {
        winningStars = 0;
        int? winningTeam = null;
        bool tie = false;

        Dictionary<int, int> teamStars = GetTeamStars(f);
        foreach ((int team, int stars) in teamStars) {
            if (winningTeam == null) {
                winningTeam = team;
                winningStars = stars;
                tie = false;
            } else if (stars > winningStars) {
                winningTeam = team;
                winningStars = stars;
                tie = false;
            } else if (stars == winningStars) {
                tie = true;   
            }
        }

        return tie ? null : winningTeam;
    }

    public static Dictionary<int,int> GetTeamStars(Frame f) {
        Dictionary<int, int> stars = f.Context.TeamStarBuffer;
        stars.Clear();

        var allPlayers = f.Filter<MarioPlayer>();
        allPlayers.UseCulling = false;
        while (allPlayers.NextUnsafe(out _, out MarioPlayer* mario)) {
            if (mario->Lives <= 0 && f.Global->Rules.IsLivesEnabled) {
                continue;
            }

            if (!stars.ContainsKey(mario->Team)) {
                stars[mario->Team] = 0;
            }

            stars[mario->Team] += mario->Stars;
        }

        return stars;
    }

    public static int GetTeamStars(Frame f, int team) {
        int sum = 0;
        var allPlayers = f.Filter<MarioPlayer>();
        allPlayers.UseCulling = false;
        while (allPlayers.NextUnsafe(out _, out MarioPlayer* mario)) {
            if (mario->Team != team
                || (mario->Lives <= 0 && f.Global->Rules.IsLivesEnabled)) {
                continue;
            }

            sum += mario->Stars;
        }

        return sum;
    }

    public static int GetFirstPlaceStars(Frame f) {
        Span<int> teamStars = stackalloc int[10];

        var allPlayers = f.Filter<MarioPlayer>();
        allPlayers.UseCulling = false;
        while (allPlayers.Next(out _, out MarioPlayer mario)) {
            if (mario.Team < teamStars.Length) {
                teamStars[mario.Team] += mario.Stars;
            }
        }

        int max = 0;
        foreach (int stars in teamStars) {
            if (stars > max) {
                max = stars;
            }
        }

        return max;
    }

    // MAX(0,$B15+(IF(stars behind >0,LOG(B$1+1, 2.71828),0)*$C15*(1-(($M$15-$M$14))/$M$15)))
    public static PowerupAsset GetRandomItem(Frame f, MarioPlayer* mario) {
        var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);

        // "Losing" variable based on ln(x+1), x being the # of stars we're behind

        int ourStars = GetTeamStars(f, mario->Team);
        int leaderStars = GetFirstPlaceStars(f);

        var rules = f.Global->Rules;
        int starsToWin = rules.StarsToWin;
        bool custom = rules.CustomPowerupsEnabled;
        bool lives = rules.IsLivesEnabled;

        bool big = stage.SpawnBigPowerups;
        bool vertical = stage.SpawnVerticalPowerups;

        bool canSpawnMega = true;
        var allPlayers = f.Filter<MarioPlayer>();
        allPlayers.UseCulling = false;
        while (allPlayers.Next(out _, out MarioPlayer otherPlayer)) {
            if (otherPlayer.CurrentPowerupState == PowerupState.MegaMushroom) {
                canSpawnMega = false;
                break;
            }
        }

        FP totalChance = 0;
        foreach (PowerupAsset powerup in f.SimulationConfig.AllPowerups) {
            if (powerup.State == PowerupState.MegaMushroom && !canSpawnMega) {
                continue;
            }

            if ((powerup.BigPowerup && !big)
                || (powerup.VerticalPowerup && !vertical)
                || (powerup.CustomPowerup && !custom)
                || (powerup.LivesOnlyPowerup && !lives)) {
                continue;
            }

            totalChance += powerup.GetModifiedChance(starsToWin, leaderStars, ourStars);
        }

        FP rand = f.RNG->Next(0, totalChance);
        foreach (PowerupAsset powerup in f.SimulationConfig.AllPowerups) {
            if (powerup.State == PowerupState.MegaMushroom && !canSpawnMega) {
                continue;
            }

            if ((powerup.BigPowerup && !big)
                || (powerup.VerticalPowerup && !vertical)
                || (powerup.CustomPowerup && !custom)
                || (powerup.LivesOnlyPowerup && !lives)) {
                continue;
            }

            FP chance = powerup.GetModifiedChance(starsToWin, leaderStars, ourStars);

            if (rand < chance) {
                return powerup;
            }

            rand -= chance;
        }

        return f.SimulationConfig.FallbackPowerup;
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
        FP width = stage.TileDimensions.x * FP._0_50;
        if (stage.IsWrappingLevel && FPMath.Abs(a.X - b.X) > width * FP._0_50) {
            a.X -= width * FPMath.Sign(a.X - b.X);
        }

        xDifference = a.X - b.X;
        return FPVector2.Distance(a, b);
    }

    public static FP WrappedDistanceSquared(VersusStageData stage, FPVector2 a, FPVector2 b) {
        FP width = stage.TileDimensions.x * FP._0_50;
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

        return (a.X > b.X ^ FPMath.Abs(a.X - b.X) > stage.TileDimensions.x * FP._0_25) ? 1 : -1;
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
        foreach (var powerup in f.SimulationConfig.AllPowerups) {
            if (powerup.State == state) {
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
            HashSet<int> teams = new();
            for (int i = 0; i < playerDataCount; i++) {
                PlayerData* pd = allPlayerDatas[i];

                if (!pd->IsSpectator && !pd->ManualSpectator) {
                    teams.Add(pd->Team);
                }
            }

            if (teams.Count < 2) {
                return false;
            }
        }

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