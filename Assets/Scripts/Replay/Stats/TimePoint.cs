#nullable enable
using Photon.Deterministic;
using Quantum;
using System;
using System.Collections.Generic;

namespace NSMB.Replay.Stats
{

    /**
     * Use this class to save information such as when a powerUP is GOtten,
     * when a star is GOtten, when a star is dropped etc.
     */
    public unsafe abstract class TimePoint : IComparable<TimePoint>
    {
        public string? AffectedPlayerName { get; protected set; }
        public int OccurenceFrame { get; private protected set; }
        public FP DeltaTime { get; private protected set; }
        public int? EndFrame { get; set; }
        public int Id;
        public static int _index { get; private protected set; }

        public int CompareTo(TimePoint? other)
        {
            if (other == null) return 1;
            return this.Id.CompareTo(other.Id);
        }

        private protected static void BasicInit(TimePoint timePoint, Frame f, MarioPlayer* mario)
        {
            timePoint.OccurenceFrame = f.Number;
            timePoint.DeltaTime = f.DeltaTime;
            timePoint.Id = _index++;
            if (mario != null) timePoint.AffectedPlayerName = f.GetPlayerData(mario->PlayerRef).PlayerNickname;
        }

        private protected static void BasicInit(TimePoint timePoint, Frame f, PlayerInfo playerInfo)
        {
            timePoint.OccurenceFrame = f.Number;
            timePoint.DeltaTime = f.DeltaTime;
            timePoint.Id = _index++;
            timePoint.AffectedPlayerName = playerInfo.PlayerName;
        }

        private protected static void BasicInit(TimePoint timePoint, Frame f, string playerName)
        {
            timePoint.OccurenceFrame = f.Number;
            timePoint.DeltaTime = f.DeltaTime;
            timePoint.Id = _index++;
            timePoint.AffectedPlayerName = playerName;
        }

        private protected static void GlobalInit(TimePoint timePoint, Frame f)
        {
            timePoint.OccurenceFrame = f.Number;
            timePoint.DeltaTime = f.DeltaTime;
            timePoint.Id = _index++;
        }

        public static void ResetIndex() {
            _index = 0;
        }
        // any extra parameters can be GOtten
    }

    public unsafe class PointCoinCollected : TimePoint
    {
        public readonly string? ItemName;
        public readonly FP? SpawnChancePercentage;
        public readonly FP? SpawnChanceRaw;
        public readonly int CoinCount;
        public readonly int CoinCountTotal;
        public readonly int CurrStarCount;
        public readonly int LeaderStars;
        public PointCoinCollected(Frame f, MarioPlayer* mario, PlayerInfo playerInfo, int coinCount, CoinItemAsset? coinItemAsset)
        {
            BasicInit(this, f, mario);
            int starsToWin = f.Global->Rules.StarsToWin; // we can get stars to win from the Replay Header
            var gamemode = f.FindAsset(f.Global->Rules.Gamemode);
            CurrStarCount = gamemode.GetTeamObjectiveCount(f, mario->GetTeam(f)) ?? -1;
            LeaderStars = gamemode.GetFirstPlaceObjectiveCount(f);
            CoinCount = coinCount;
            CoinCountTotal = ++playerInfo.Coins;
            if (coinItemAsset is CoinItemAsset coinItem)
            {
                ItemName = coinItem.name;
                SpawnChanceRaw = gamemode.GetItemSpawnWeight(f, coinItem, CurrStarCount);
                FP sum = 0;
                foreach (var currCoinItemRef in gamemode.AllCoinItems)
                {
                    CoinItemAsset currCoinItemAsset = f.FindAsset(currCoinItemRef);

                    var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);

                    if (!coinItem.CanSpawn(f, false)) { continue; }
                    sum += gamemode.GetItemSpawnWeight(f, currCoinItemAsset, CurrStarCount);
                }
                SpawnChancePercentage = SpawnChanceRaw / sum * 100;
            }
        }
    }

    public unsafe class PointStarCollected : TimePoint
    {
        public readonly int StarCount;
        public readonly int TotalStarCount;
        public PointStarCollected(Frame f, MarioPlayer* mario, PlayerInfo info, int starCount)
        {
            BasicInit(this, f, mario);
            StarCount = starCount;
            TotalStarCount = ++info.Stars;
        }
    }

    public unsafe class PointDamage : TimePoint
    {
        public readonly PowerupState NewState;
        public PointDamage(Frame f, MarioPlayer* mario)
        {
            OccurenceFrame = f.Number;
            DeltaTime = f.DeltaTime;
            NewState = mario->CurrentPowerupState;
            Id = _index++;
        }
    }

    public unsafe class PointDeath : TimePoint
    {
        public readonly int LivesRemaining;
        public readonly int Ping;
        public PointDeath(Frame f, MarioPlayer* mario, PlayerInfo playerInfo, int ping)
        {
            BasicInit(this, f, mario);
            LivesRemaining = mario->Lives;
            Ping = ping;
        }
    }

    public unsafe class PointKnockback : TimePoint
    {
        public readonly int StarsDropped;
        public readonly string? AttackerName;
        public readonly KnockbackStrength KnockbackStrength;
        public PointKnockback(Frame f, MarioPlayer* mario, EntityRef attacker, int starDropCount, KnockbackStrength knockbackStrength)
        {
            BasicInit(this, f, mario);
            var attackerMario = f.Unsafe.GetPointer<MarioPlayer>(attacker);
            var attackerPlayer = f.GetPlayerData(attackerMario->PlayerRef);
            AttackerName = attackerPlayer.PlayerNickname;
            KnockbackStrength = knockbackStrength;
            StarsDropped = starDropCount;
        }
    }

    public unsafe class PointStarLoss : TimePoint
    {
        public enum StarLossCause
        {
            Unknown,
            Death,
            Stomp,
            CollisionBump,
            HipDrop,
            Fireball,
            BlueShell,
            Iceball,
            Damage
        }
        public StarLossCause Reason;
        public string? AttackerName;
        public readonly int StarAmount;
        public readonly int StarDropCount;
        public readonly int TotalStarsLost;
        public PointStarLoss(Frame f, MarioPlayer* mario, PlayerInfo victimInfo, int starDropCount, StarLossCause reason, EntityRef attacker)
        {
            BasicInit(this, f, mario);
            StarAmount = mario->GamemodeData.StarChasers->Stars;
            StarDropCount = starDropCount;
            Reason = reason;
            TotalStarsLost = victimInfo.StarsDropped += starDropCount;
            if (attacker == EntityRef.None) return;

            if (f.Unsafe.TryGetPointer<MarioPlayer>(attacker, out var AttackerMario))
            {
                var attackerPlayer = f.GetPlayerData(AttackerMario->PlayerRef);
                AttackerName = attackerPlayer.PlayerNickname;
            }
        }
    }

    public unsafe class PointCombo : TimePoint
    {
        // these are the things that are in the combo
        // we reuse TimePoints for this.
        public readonly List<TimePoint> ComboElements = new();
        public readonly List<int> StarsLost = new();
        public readonly List<int> TotalStarsLost = new();
        public PointCombo(Frame f, MarioPlayer* mario, TimePoint comboElement, int starsLost)
        {
            BasicInit(this, f, mario);
            ComboElements.Add(comboElement);
            StarsLost.Add(starsLost);
            int totalStarsLost = 0;
            for (int i = 0; i < StarsLost.Count; i++)
            {
                totalStarsLost += StarsLost[i];
            }
            TotalStarsLost.Add(totalStarsLost);
        }
    }

    public unsafe class PointPowerChange : TimePoint
    {
        public readonly PowerupState PowerupState;
        public PointPowerChange(Frame f, MarioPlayer* mario)
        {
            BasicInit(this, f, mario);
            PowerupState = mario->CurrentPowerupState;
        }
    }

    public unsafe class PointStarmanChange : TimePoint
    {
        public PointStarmanChange(Frame f, MarioPlayer* mario)
        {
            BasicInit(this, f, mario);
        }
    }

    public unsafe class PointReserveChange : TimePoint
    {
        // allow for null
        public readonly PowerupAsset Powerup;
        public PointReserveChange(Frame f, MarioPlayer* mario)
        {
            BasicInit(this, f, mario);
            Powerup = f.FindAsset(mario->ReserveItem);
        }
    }

    public unsafe class PointPowerupCollect : TimePoint
    {
        public readonly PowerupReserveResult ReserveResult;
        public readonly string Powerup;
        public PointPowerupCollect(Frame f, MarioPlayer* mario, PowerupReserveResult reserveResult, PowerupAsset powerUPAsset)
        {
            BasicInit(this, f, mario);
            ReserveResult = reserveResult;
            Powerup = powerUPAsset.Path.Split('/')[^1];
        }
    }

    public unsafe class PointBigCollectableSpawned : TimePoint
    {
        public readonly int AttemptedSpawnCount;
        public readonly int SuccessfulSpawnCount;
        public readonly int FailedSpawnCount; // when stars are blocked

        public readonly int Spawnpoints;
        public readonly int PositionIndex;
        public readonly int UsedSpawns;
        public readonly bool WasBlocked;
        public readonly FPVector2 Coordinates;
        public readonly List<string> BlockingPlayers = new();
        public string? CollectingPlayer; // if a player collected the big star this is their name
        public PointBigCollectableSpawned(Frame f, int usedSpawns, int index, bool blocked, FPVector2 coordinates, ref GlobalInfo globalReplayInfo, VersusStageData stage)
        {
            GlobalInit(this, f);
            PositionIndex = index;
            UsedSpawns = usedSpawns;
            PositionIndex = index;
            WasBlocked = blocked;
            Spawnpoints = stage.BigStarSpawnpoints.Length;
            Coordinates = coordinates;

            AttemptedSpawnCount = ++globalReplayInfo.AttemptedStarSpawns;
            if (!blocked) ++globalReplayInfo.SuccessfulStarSpawns;
            else ++globalReplayInfo.FailedStarSpawns;

            SuccessfulSpawnCount = globalReplayInfo.SuccessfulStarSpawns;
            FailedSpawnCount = globalReplayInfo.FailedStarSpawns;
        }
    }

}
