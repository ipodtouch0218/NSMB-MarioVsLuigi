#nullable enable
using NSMB.UI.Translation;
using NSMB.Utilities;
using Photon.Deterministic;
using Quantum;
using System;
using System.Collections.Generic;
using System.Text;

namespace NSMB.Replay.Stats {

    /**
     * Use this class to save information such as when a powerUP is GOtten,
     * when a star is GOtten, when a star is dropped etc.
     */
    public unsafe abstract class TimePoint : IComparable<TimePoint> {
        //---object-specific variables
        public PlayerRef? PlayerRef { get; protected set; }
        public string? AffectedPlayerName { get; protected set; }
        public int OccurenceFrame { get; private protected set; }
        public FP DeltaTime { get; private protected set; }
        public int? EndFrame { get; set; }
        public int Id;
        public byte[]? SerializedFrame { get; protected set; }
        public ReplayStatsRecorder? StatsRecorder { get; protected set; }

        //---for assigning the index of time point
        public static int _index { get; private protected set; }

        public int CompareTo(TimePoint? other) {
            if (other == null) return 1;
            return this.Id.CompareTo(other.Id);
        }


        //---abstractions, overrideables for time point enteries
        public virtual void SetTimeText(TranslationManager tm, StringBuilder stringBuilder) {
            if (StatsRecorder == null) {
                stringBuilder.Append("");
                return;
            }
            stringBuilder.Append($"@ {EventManager.FrameToTime(OccurenceFrame, StatsRecorder.ReplayStart, DeltaTime)}");

            if (EndFrame != null) {
                stringBuilder.Append('-');

                if (EndFrame > -1) {
                    stringBuilder.Append(EventManager.FrameToTime(EndFrame.Value, StatsRecorder.ReplayStart, DeltaTime));
                }
            }
        }


        public virtual void SetSymbolsText(TranslationManager tm, StringBuilder stringBuilder) { }

        public virtual void SetAdditionalText(TranslationManager tm, StringBuilder stringBuilder) { }

        public abstract void SetDescriptionText(TranslationManager tm, StringBuilder stringBuilder);

        //---bases for generating a new time point
        private protected static void BasicInit(TimePoint timePoint, ReplayStatsRecorder statsRecorder, Frame f, MarioPlayer* mario) {
            timePoint.StatsRecorder = statsRecorder;
            timePoint.OccurenceFrame = f.Number;
            timePoint.DeltaTime = f.DeltaTime;
            timePoint.Id = _index++;
            timePoint.PlayerRef = mario->PlayerRef;
            timePoint.SerializedFrame = f.Serialize(DeterministicFrameSerializeMode.Serialize);
            if (mario != null) {
                timePoint.AffectedPlayerName = f.GetPlayerData(mario->PlayerRef).PlayerNickname;
            }
        }

        private protected static void GlobalInit(TimePoint timePoint, ReplayStatsRecorder statsRecorder, Frame f) {
            timePoint.StatsRecorder = statsRecorder;
            timePoint.OccurenceFrame = f.Number;
            timePoint.DeltaTime = f.DeltaTime;
            timePoint.Id = _index++;
            timePoint.SerializedFrame = f.Serialize(DeterministicFrameSerializeMode.Serialize);
        }

        //---static methods
        public static void ResetIndex() {
            _index = 0;
        }
        // any extra parameters can be GOtten
    }

    public unsafe class PointCoinCollected : TimePoint {
        public readonly CoinItemAsset? CoinItem;
        public readonly FP? SpawnChancePercentage, SpawnChanceRaw;
        public readonly int CoinCount, CoinCountTotal, CurrStarCount, LeaderStars;
        public readonly FP AverageStarCount;

        public PointCoinCollected(ReplayStatsRecorder statsRecorder, Frame f, MarioPlayer* mario, PlayerInfo playerInfo, int coinCount, CoinItemAsset? coinItemAsset) {
            BasicInit(this, statsRecorder, f, mario);
            int starsToWin = f.Global->Rules.StarsToWin; // we can get stars to win from the Replay Header
            var gamemode = f.FindAsset(f.Global->Rules.Gamemode);
            CurrStarCount = gamemode.GetTeamObjectiveCount(f, mario->GetTeam(f)) ?? -1;
            LeaderStars = gamemode.GetFirstPlaceObjectiveCount(f);
            AverageStarCount = gamemode.GetAverageObjectiveCount(f);
            CoinCount = coinCount;
            CoinCountTotal = ++playerInfo.Coins;
            if (coinItemAsset is CoinItemAsset coinItem) {
                CoinItem = coinItem;
                SpawnChanceRaw = gamemode.GetItemSpawnWeight(f, coinItem, CurrStarCount);
                FP sum = 0;
                foreach (var currCoinItemRef in gamemode.AllCoinItems) {
                    CoinItemAsset currCoinItemAsset = f.FindAsset(currCoinItemRef);

                    var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);

                    if (!coinItem.CanSpawn(f, false)) { continue; }
                    sum += gamemode.GetItemSpawnWeight(f, currCoinItemAsset, CurrStarCount);
                }
                SpawnChancePercentage = SpawnChanceRaw / sum * 100;
            }
        }

        public override void SetDescriptionText(TranslationManager tm, StringBuilder stringBuilder) {
            if (CoinItem != null) {
                stringBuilder.Append(tm.GetTranslationWithReplacements("ui.replay.stats.entry.coin.itemspawn", "item", CoinItem.name));
            } else {
                stringBuilder.Append(tm.GetTranslation("ui.replay.stats.entry.coin.collected"));
            }
        }

        public override void SetSymbolsText(TranslationManager tm, StringBuilder stringBuilder) {
            if (CoinItem != null) {
                stringBuilder.Append("<sprite name=room_powerups>");
            }

            stringBuilder.Append("<sprite name=room_coins>").Append(Utils.GetSymbolString(CoinCount.ToString(), Utils.smallSymbols));
        }

        public override void SetAdditionalText(TranslationManager tm, StringBuilder stringBuilder) {
            if (CoinItem != null) {
                stringBuilder.Append(tm.GetTranslationWithReplacements("ui.replay.stats.entry.spawnchance", "chance", $"{(float)SpawnChancePercentage.GetValueOrDefault():0.00}"));
            }
        }
    }

    public unsafe class PointStarCollected : TimePoint {
        public readonly int StarCount, TotalStarCount;
        public PointStarCollected(ReplayStatsRecorder statsRecorder, Frame f, MarioPlayer* mario, PlayerInfo info, int starCount) {
            BasicInit(this, statsRecorder, f, mario);
            StarCount = starCount;
            TotalStarCount = ++info.Stars;
        }


        public override void SetSymbolsText(TranslationManager tm, StringBuilder stringBuilder) {
            stringBuilder.Append("<sprite name=room_stars>").Append(Utils.GetSymbolString(StarCount.ToString(), Utils.smallSymbols));
        }

        public override void SetDescriptionText(TranslationManager tm, StringBuilder stringBuilder) {
            stringBuilder.Append(tm.GetTranslationWithReplacements("ui.replay.stats.entry.stars", "total", TotalStarCount.ToString()));
        }
    }

    public unsafe class PointDamage : TimePoint {
        public readonly PowerupState NewState;
        public PointDamage(ReplayStatsRecorder statsRecorder, Frame f, MarioPlayer* mario) {
            OccurenceFrame = f.Number;
            DeltaTime = f.DeltaTime;
            NewState = mario->CurrentPowerupState;
            Id = _index++;
        }

        public override void SetDescriptionText(TranslationManager tm, StringBuilder stringBuilder) {
            stringBuilder.Append($"Damage");
        }
    }

    public unsafe class PointDeath : TimePoint {
        public readonly int LivesRemaining, Ping;
        public readonly DeathCause Reason;
        public readonly string? AttackerName;
        public enum DeathCause {
            Pit,
            Lava,
            Poison,
            Enemy,
            Disconnect,
            OtherPlayer,
            Unknown
        }
        public PointDeath(ReplayStatsRecorder statsRecorder, Frame f, MarioPlayer* mario, PlayerInfo playerInfo, DeathCause reason, int ping, string? attackerName = null) {
            BasicInit(this, statsRecorder, f, mario);
            LivesRemaining = mario->Lives;
            Reason = reason;
            Ping = ping;
            AttackerName = attackerName;
        }

        public override void SetDescriptionText(TranslationManager tm, StringBuilder stringBuilder) {
            var translationKey = "ui.replay.stats.entry.deaths."+Reason.ToString().ToLower();
            var translation = AttackerName == null ?
                tm.GetTranslation(translationKey) :
                tm.GetTranslationWithReplacements(translationKey, "attacker", AttackerName);
            stringBuilder.Append(translation);
        }

        public override void SetSymbolsText(TranslationManager tm, StringBuilder stringBuilder) {
            if (StatsRecorder != null && StatsRecorder.ReplayFile.Header.Rules.Lives > 0) {
                stringBuilder.Append("<sprite name=room_lives>").Append(Utils.GetSymbolString(LivesRemaining.ToString(), Utils.smallSymbols));
            }
        }

        public override void SetAdditionalText(TranslationManager tm, StringBuilder stringBuilder) {
            stringBuilder.Append($"Ping: {Ping}ms");
        }
    }

    public unsafe class PointKnockback : TimePoint {
        public readonly int StarsDropped;
        public readonly string AttackerName;
        public readonly PlayerRef AttackerRef;
        public readonly KnockbackStrength KnockbackStrength;
        public PointKnockback(ReplayStatsRecorder statsRecorder, Frame f, MarioPlayer* mario, EntityRef attacker, int starDropCount, KnockbackStrength knockbackStrength) {
            BasicInit(this, statsRecorder, f, mario);
            var attackerMario = f.Unsafe.GetPointer<MarioPlayer>(attacker);
            var attackerPlayer = f.GetPlayerData(attackerMario->PlayerRef);
            AttackerName = attackerPlayer.PlayerNickname;
            KnockbackStrength = knockbackStrength;
            StarsDropped = starDropCount;
            AttackerRef = attackerMario->PlayerRef;
        }

        public override void SetDescriptionText(TranslationManager tm, StringBuilder stringBuilder) {
            string translationString = "ui.replay.stats.entry.knockback." + KnockbackStrength.ToString().ToLower();
            stringBuilder.Append(tm.GetTranslationWithReplacements(translationString, "victim", AffectedPlayerName, "attacker", AttackerName));
        }

        public override void SetSymbolsText(TranslationManager tm, StringBuilder stringBuilder) {
            stringBuilder.Append("<sprite name=room_stars>").Append(Utils.GetSymbolString(StarsDropped.ToString(), Utils.smallSymbols));
        }
    }

    public unsafe class PointStarLoss : TimePoint {
        public enum StarLossCause {
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
        public readonly int StarAmount, StarDropCount, TotalStarsLost;
        public PointStarLoss(ReplayStatsRecorder statsRecorder, Frame f, MarioPlayer* mario, PlayerInfo victimInfo, int starDropCount, StarLossCause reason, EntityRef attacker) {
            BasicInit(this, statsRecorder, f, mario);
            var gamemode = f.FindAsset(f.Global->Rules.Gamemode);
            StarAmount = gamemode.GetObjectiveCount(f, mario);
            StarDropCount = starDropCount;
            Reason = reason;
            TotalStarsLost = victimInfo.StarsDropped += starDropCount;
            if (attacker == EntityRef.None) return;

            if (f.Unsafe.TryGetPointer<MarioPlayer>(attacker, out var AttackerMario)) {
                var attackerPlayer = f.GetPlayerData(AttackerMario->PlayerRef);
                AttackerName = attackerPlayer.PlayerNickname;
            }
        }

        public override void SetDescriptionText(TranslationManager tm, StringBuilder stringBuilder) {
            stringBuilder.Append($"Lost a star due to {Reason}");
        }

        public override void SetSymbolsText(TranslationManager tm, StringBuilder stringBuilder) {
            stringBuilder.Append("X").Append(Utils.GetSymbolString(StarAmount.ToString(), Utils.smallSymbols));
        }
    }

    public unsafe class PointCombo : TimePoint {
        // these are the things that are in the combo
        // we reuse TimePoints for this.
        
        // tUPle, first is the elemnt, second is stars lost third is total stars lost
        public readonly List<(TimePoint Element, int StarsLost, int TotalStarsLost)> ComboElements = new();
        public Dictionary<PlayerRef, string> GetParticipants() {
            Dictionary<PlayerRef, string> attackerNames = new();

            foreach (var element in ComboElements) {
                if (element.Element is PointKnockback kbPoint) {
                    if (!attackerNames.ContainsKey(kbPoint.AttackerRef)) {
                        attackerNames.Add(kbPoint.AttackerRef, kbPoint.AttackerName);
                    }
                }
            }

            return attackerNames;
        }

        public int TotalStarsAfterCombo() {
            int totalStars = 0;
            foreach (var element in ComboElements) {
                totalStars += element.StarsLost;
            }
            return totalStars;
        }
        public PointCombo(ReplayStatsRecorder statsRecorder, Frame f, MarioPlayer* mario, TimePoint comboElement, int starsLost) {
            BasicInit(this, statsRecorder, f, mario);
            int totalStarsLost = 0;
            foreach (var element in ComboElements) {
                totalStarsLost += element.StarsLost;
            }
            ComboElements.Add((comboElement, starsLost, totalStarsLost));
        }

        public override void SetDescriptionText(TranslationManager tm, StringBuilder stringBuilder) {
            stringBuilder.Append(tm.GetTranslationWithReplacements("ui.replay.stats.entry.combo", "count", ComboElements.Count.ToString()));
        }

        public override void SetSymbolsText(TranslationManager tm, StringBuilder stringBuilder) {
            stringBuilder.Append("<sprite name=room_stars>").Append(Utils.GetSymbolString(TotalStarsAfterCombo().ToString(), Utils.smallSymbols));
        }

        public override void SetAdditionalText(TranslationManager tm, StringBuilder stringBuilder) {
            var attackers = GetParticipants();
            var attackersStr = string.Join(", ", attackers);
            stringBuilder.Append(attackersStr);
        }
    }

    public unsafe class PointPowerChange : TimePoint {
        public readonly PowerupState PowerupState;
        public PointPowerChange(ReplayStatsRecorder statsRecorder, Frame f, MarioPlayer* mario) {
            BasicInit(this, statsRecorder, f, mario);
            PowerupState = mario->CurrentPowerupState;
        }

        public override void SetDescriptionText(TranslationManager tm, StringBuilder stringBuilder) {
            stringBuilder.Append($"Has powerup state {PowerupState}");
        }
    }

    public unsafe class PointStarmanChange : TimePoint {
        public PointStarmanChange(ReplayStatsRecorder statsRecorder, Frame f, MarioPlayer* mario) {
            BasicInit(this, statsRecorder, f, mario);
        }

        public override void SetDescriptionText(TranslationManager tm, StringBuilder stringBuilder) {
            stringBuilder.Append($"Starman invincible");
        }
    }

    public unsafe class PointReserveChange : TimePoint {
        // allow for null
        public readonly PowerupAsset Powerup;
        public PointReserveChange(ReplayStatsRecorder statsRecorder, Frame f, MarioPlayer* mario) {
            BasicInit(this, statsRecorder, f, mario);
            Powerup = f.FindAsset(mario->ReserveItem);
        }

        public override void SetDescriptionText(TranslationManager tm, StringBuilder stringBuilder) {
            stringBuilder.Append($"Reserve powerup is {Powerup.name}");
        }
    }

    public unsafe class PointPowerupCollect : TimePoint {
        public readonly PowerupReserveResult ReserveResult;
        public readonly PowerupAsset Powerup;
        public PointPowerupCollect(ReplayStatsRecorder statsRecorder, Frame f, MarioPlayer* mario, PowerupReserveResult reserveResult, PowerupAsset powerupAsset) {
            BasicInit(this, statsRecorder, f, mario);
            ReserveResult = reserveResult;
            Powerup = powerupAsset;
        }

        public override void SetDescriptionText(TranslationManager tm, StringBuilder stringBuilder) {
            stringBuilder.Append($"Collected a {Powerup.name}");
        }
    }

    public unsafe class PointBigCollectableSpawned : TimePoint {
        public readonly int AttemptedSpawnCount, SuccessfulSpawnCount, FailedSpawnCount;
        public readonly int Spawnpoints, PositionIndex, UsedSpawns;

        public readonly bool WasBlocked;
        public readonly FPVector2 Coordinates;
        public readonly List<string> BlockingPlayers = new();
        public string? CollectingPlayer; // if a player collected the big star this is their name
        public PointBigCollectableSpawned(ReplayStatsRecorder statsRecorder, Frame f, int usedSpawns, int index, bool blocked, FPVector2 coordinates, ref GlobalInfo globalReplayInfo, VersusStageData stage) {
            GlobalInit(this, statsRecorder, f);
            PositionIndex = index;
            UsedSpawns = usedSpawns;
            WasBlocked = blocked;
            Spawnpoints = stage.BigStarSpawnpoints.Length;
            Coordinates = coordinates;

            AttemptedSpawnCount = ++globalReplayInfo.AttemptedStarSpawns;
            if (!blocked) {
                ++globalReplayInfo.SuccessfulStarSpawns;
            } else {
                ++globalReplayInfo.FailedStarSpawns;
            }

            SuccessfulSpawnCount = globalReplayInfo.SuccessfulStarSpawns;
            FailedSpawnCount = globalReplayInfo.FailedStarSpawns;
        }

        public override void SetDescriptionText(TranslationManager tm, StringBuilder stringBuilder) {
            stringBuilder.Append(tm.GetTranslationWithReplacements("ui.replay.stats.entry.bigcollectablespawn", "position", PositionIndex.ToString(), "spawnpoints", Spawnpoints.ToString()));
        }

        public override void SetAdditionalText(TranslationManager tm, StringBuilder stringBuilder) {
            stringBuilder.Append(CollectingPlayer);
        }

        public override void SetSymbolsText(TranslationManager tm, StringBuilder stringBuilder) {
            stringBuilder.Append("X").Append(Utils.GetSymbolString(FailedSpawnCount.ToString(), Utils.smallSymbols));
            stringBuilder.Append("<sprite name=room_stars>").Append(Utils.GetSymbolString(SuccessfulSpawnCount.ToString(), Utils.smallSymbols));
        }
    }
}
