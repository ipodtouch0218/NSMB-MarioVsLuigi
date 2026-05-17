#nullable enable
using NSMB.UI.Translation;
using NSMB.Utilities;
using Photon.Deterministic;
using Quantum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace NSMB.Replay.Stats {

    /**
     * Use this class to save information such as when a powerUP is GOtten,
     * when a star is GOtten, when a star is dropped etc.
     */
    public unsafe abstract class TimePoint : IComparable<TimePoint> {
        //---object-specific variables
        public int? EndFrame { get; set; }

        //---properties (read-only)
        public readonly PlayerRef? PlayerRef;
        public readonly string? AffectedPlayerName;
        public readonly int OccurenceFrame;
        public readonly FP DeltaTime;
        public readonly int Id;
        public readonly byte[] SerializedFrame;
        public readonly ReplayStatsRecorder StatsRecorder;

        //---static
        private static int _index;
        public const string translationPrefix = "ui.replay.stats.entry.";

        //---enums
        public enum DisplayArgs {
            Normal,
            FromAttacker
        }


        public int CompareTo(TimePoint? other) {
            if (other == null) return 1;
            return this.Id.CompareTo(other.Id);
        }

        //---bases for generating a new time point
        // global init
        public TimePoint(ReplayStatsRecorder stats, Frame f) {
            StatsRecorder = stats;
            OccurenceFrame = f.Number;
            DeltaTime = f.DeltaTime;
            Id = _index++;
            SerializedFrame = f.Serialize(DeterministicFrameSerializeMode.Serialize);
        }

        // basic init - per player
        public TimePoint(ReplayStatsRecorder stats, Frame f, MarioPlayer* mario) : this(stats, f) {
            PlayerRef = mario->PlayerRef;
            if (mario != null) {
                AffectedPlayerName = f.GetPlayerData(mario->PlayerRef).PlayerNickname;
            }
        }


        //---abstractions, overrideables for time point enteries
        public abstract void SetDescriptionText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg);

        public virtual void SetTimeText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
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

        public virtual void SetSymbolsText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) { }

        public virtual void SetAdditionalText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) { }

        public virtual string? GetTooltip(TranslationManager tm, DisplayArgs displayArg) => null;

        //---static methods
        public static void ResetIndex() => _index = 0;

        // any extra parameters can be GOtten
    }

    public unsafe class PointCoinCollected : TimePoint {
        public readonly CoinItemAsset? CoinItem;
        public readonly FP? SpawnChancePercentage, SpawnChanceRaw;
        public readonly int CoinCount, CoinCountTotal, CurrStarCount, LeaderStars;
        public readonly FP AverageStarCount;

        public PointCoinCollected(ReplayStatsRecorder statsRecorder, Frame f, MarioPlayer* mario, PlayerInfo playerInfo, int coinCount, CoinItemAsset? coinItemAsset) : base(statsRecorder, f, mario) {
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

                    if (!currCoinItemAsset.CanSpawn(f, false)) {
                        continue;
                    }
                    sum += gamemode.GetItemSpawnWeight(f, currCoinItemAsset, CurrStarCount);
                }
                SpawnChancePercentage = SpawnChanceRaw / sum * 100;
            }
        }

        public override void SetDescriptionText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) => stringBuilder.Append(tm.GetTranslation("ui.replay.stats.entry.coincollected"));

        public override void SetSymbolsText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
            if (CoinItem != null) {
                stringBuilder.Append("<sprite name=room_powerups>");
            }

            stringBuilder.Append("<sprite name=room_coins>").Append(Utils.GetSymbolString(CoinCount.ToString(), Utils.smallSymbols));
        }

        public override string? GetTooltip(TranslationManager tm, DisplayArgs displayArg) {
            if (CoinItem == null) {
                return null;
            }
            var itemTranslation = tm.GetTranslation(CoinItem.TranslationKey);
            return tm.GetTranslationWithReplacements(translationPrefix+"tooltip.randomspawn", "item", itemTranslation, "chance", $"{ (float) SpawnChancePercentage.GetValueOrDefault():0.00}");
        }
    }

    public unsafe class PointStarCollected : TimePoint {
        public readonly int StarCount, TotalStarCount;
        public PointStarCollected(ReplayStatsRecorder statsRecorder, Frame f, MarioPlayer* mario, PlayerInfo info, int starCount) : base(statsRecorder, f, mario) {
            StarCount = starCount;
            TotalStarCount = ++info.Stars;
        }


        public override void SetSymbolsText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) => stringBuilder.Append("<sprite name=room_stars>").Append(Utils.GetSymbolString(StarCount.ToString(), Utils.smallSymbols));

        public override void SetDescriptionText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) => stringBuilder.Append(tm.GetTranslationWithReplacements("ui.replay.stats.entry.starscollected", "total", TotalStarCount.ToString()));
    }

    public unsafe class PointDamage : TimePoint {
        public readonly PowerupState NewState;
        public readonly DamageCause Reason;
        public readonly string? AttackerName;
        public readonly PlayerRef? AttackerRef;
        public enum DamageCause {
            Enemy,
            Shell,
            Starman,
            MegaMushroom,
            BlueShell,
            Explode
        }

        public PointDamage(ReplayStatsRecorder statsRecorder, Frame f, MarioPlayer* mario, DamageCause reason, string? attackerName, PlayerRef? attackerRef) : base(statsRecorder, f, mario) {
            NewState = mario->CurrentPowerupState;
            Reason = reason;
            AttackerName = attackerName;
            AttackerRef = attackerRef;
        }

        public override void SetDescriptionText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) => stringBuilder.Append(tm.GetTranslation("ui.replay.stats.entry.damage."+Reason.ToString().ToLower()));

        public override void SetAdditionalText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
            switch (displayArg) {
            case DisplayArgs.Normal:
                if (AttackerName != null) {
                    stringBuilder.Append(AttackerName);
                }
                break;
            case DisplayArgs.FromAttacker:
                if (AffectedPlayerName != null) {
                    stringBuilder.Append(AffectedPlayerName);
                }
                break;
            }
        }
    }

    public unsafe class PointDeath : TimePoint {
        public readonly int LivesRemaining, Ping;
        public readonly DeathCause Reason;
        public readonly string? AttackerName;
        public readonly PlayerRef? AttackerRef;
        public enum DeathCause {
            Enemy,
            Shell,
            Starman,
            MegaMushroom,
            BlueShell,
            Explode,
            Pit,
            Lava,
            Poison,
            Disconnect
        }

        public PointDeath(ReplayStatsRecorder statsRecorder, Frame f, MarioPlayer* mario, PlayerInfo playerInfo, DeathCause reason, int ping, string? attackerName, PlayerRef? attackRef) : base(statsRecorder, f, mario) {
            LivesRemaining = mario->Lives;
            Reason = reason;
            Ping = ping;
            AttackerName = attackerName;
            AttackerRef = attackRef;
        }

        public override void SetDescriptionText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) => stringBuilder.Append(tm.GetTranslation("ui.replay.stats.entry.deaths."+Reason.ToString().ToLower()));

        public override void SetSymbolsText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
            if (StatsRecorder != null && StatsRecorder.ReplayFile.Header.Rules.Lives > 0) {
                stringBuilder.Append("<sprite name=room_lives>").Append(Utils.GetSymbolString(LivesRemaining.ToString(), Utils.smallSymbols));
            }
        }

        public override void SetAdditionalText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
            switch (displayArg) {
            case DisplayArgs.Normal:
                if (AttackerName != null) {
                    stringBuilder.Append(AttackerName);
                }
                break;
            case DisplayArgs.FromAttacker:
                if (AffectedPlayerName != null) {
                    stringBuilder.Append(AffectedPlayerName);
                }
                break;
            }
        }

        public override string? GetTooltip(TranslationManager tm, DisplayArgs displayArg) => $"Ping: {Ping}ms";
    }

    public unsafe class PointKnockback : TimePoint {
        public readonly int StarsDropped;
        public readonly string AttackerName;
        public readonly PlayerRef AttackerRef;
        public readonly KnockbackStrength KnockbackStrength;
        public PointKnockback(ReplayStatsRecorder statsRecorder, Frame f, MarioPlayer* mario, EntityRef attacker, int starDropCount, KnockbackStrength knockbackStrength) : base(statsRecorder, f, mario) {
            var attackerMario = f.Unsafe.GetPointer<MarioPlayer>(attacker);
            var attackerPlayer = f.GetPlayerData(attackerMario->PlayerRef);
            AttackerName = attackerPlayer.PlayerNickname;
            KnockbackStrength = knockbackStrength;
            StarsDropped = starDropCount;
            AttackerRef = attackerMario->PlayerRef;
        }

        public override void SetTimeText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
            base.SetTimeText(tm, stringBuilder, displayArg);
            if (EndFrame != null && EndFrame > 0) {
                stringBuilder.Append($" ({EndFrame - OccurenceFrame}F)");
            }
        }

        public override void SetDescriptionText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
            string recieveOrDealt = displayArg == DisplayArgs.FromAttacker ? "dealt" : "recieved";
            string translationString = translationPrefix+"knockback." + recieveOrDealt + KnockbackStrength.ToString().ToLower();
            stringBuilder.Append(tm.GetTranslationWithReplacements(translationString, "victim", AffectedPlayerName));
        }

        public override void SetAdditionalText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
            switch (displayArg) {
            case DisplayArgs.Normal:
                if (AttackerName != null) {
                    stringBuilder.Append(AttackerName);
                }
                break;
            case DisplayArgs.FromAttacker:
                if (AffectedPlayerName != null) {
                    stringBuilder.Append(AffectedPlayerName);
                }
                break;
            }
        }

        public override void SetSymbolsText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
            var color = Color.red;
            stringBuilder.Append("<sprite name=\"room_stars\" color=#").Append(Utils.ColorToHex(color, false)).Append('>');
            stringBuilder.Append(Utils.GetSymbolString(StarsDropped.ToString(), Utils.smallSymbols, Color.red));
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
        public PointStarLoss(ReplayStatsRecorder statsRecorder, Frame f, MarioPlayer* mario, PlayerInfo victimInfo, int starDropCount, StarLossCause reason, EntityRef attacker) : base(statsRecorder, f, mario) {
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

        public override void SetDescriptionText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) => stringBuilder.Append($"Lost a star due to {Reason}");

        public override void SetSymbolsText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) => stringBuilder.Append("X").Append(Utils.GetSymbolString(StarAmount.ToString(), Utils.smallSymbols));
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

        public bool EndsInDeath() {
            foreach (var element in ComboElements) {
                if (element.Element is PointDeath) {
                    return true;
                }
            }

            return false;
        }

        public int TotalStarsAfterCombo() {
            int totalStars = 0;
            foreach (var element in ComboElements) {
                totalStars += element.StarsLost;
            }
            return totalStars;
        }

        public PointCombo(ReplayStatsRecorder statsRecorder, Frame f, MarioPlayer* mario, TimePoint comboElement, int starsLost) : base(statsRecorder, f, mario) => AddComboElement(comboElement, starsLost);

        public void AddComboElement(TimePoint timePoint, int starsLost) {
            int totalStarsLost = starsLost;
            foreach (var element in ComboElements) {
                totalStarsLost += element.StarsLost;
            }
            ComboElements.Add((timePoint, starsLost, totalStarsLost));
        }

        public override void SetTimeText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
            if (StatsRecorder == null) {
                return;
            }

            stringBuilder.Append(string.Join(", ", ComboElements.Select(
                c => EventManager.FrameToTime(c.Element.OccurenceFrame, StatsRecorder.ReplayStart, c.Element.DeltaTime))
            ));
        }

        public override void SetDescriptionText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) => stringBuilder.Append(tm.GetTranslationWithReplacements("ui.replay.stats.entry.combo", "victim", AffectedPlayerName, "count", ComboElements.Count.ToString()));

        public override void SetSymbolsText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
            var color = Color.red;
            if (EndsInDeath()) {
                stringBuilder.Append("<sprite name=\"room_lives\" color=#").Append(Utils.ColorToHex(color, false)).Append('>');
            }
            stringBuilder.Append("<sprite name=\"room_stars\" color=#").Append(Utils.ColorToHex(color, false)).Append('>');
            stringBuilder.Append(Utils.GetSymbolString(TotalStarsAfterCombo().ToString(), Utils.smallSymbols, Color.red));
        }

        public override void SetAdditionalText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
            var attackers = GetParticipants();
            if (attackers.Count > 1) {
                stringBuilder.Append(tm.GetTranslationWithReplacements(translationPrefix+"combo.participents", "number", attackers.Count.ToString()));
            } else {
                stringBuilder.Append(attackers.Values.First());
            }
        }

        public override string? GetTooltip(TranslationManager tm, DisplayArgs displayArg) {
            StringBuilder sb = new();
            sb.AppendLine(tm.GetTranslation(translationPrefix + "combo.tooltip.parts"));
            foreach (var (Element, _, _) in ComboElements) {
                if (Element is PointKnockback kb) {
                    int frame = kb.OccurenceFrame - OccurenceFrame;
                    sb.Append(tm.GetTranslationWithReplacements(translationPrefix + "combo.tooltip.knockback", "attacker", kb.AttackerName, "framenumber", frame.ToString())).Append(" "+kb.StarsDropped+"★");
                } else if (Element is PointDamage dmg) {
                    dmg.SetDescriptionText(tm, sb, displayArg);
                } else if (Element is PointDeath death) {
                    death.SetDescriptionText(tm, sb, displayArg);
                }
            }

            return sb.ToString();
        }
    }

    public unsafe class PointPowerChange : TimePoint {
        public readonly PowerupState PowerupState;
        public PointPowerChange(ReplayStatsRecorder statsRecorder, Frame f, MarioPlayer* mario) : base(statsRecorder, f, mario) => PowerupState = mario->CurrentPowerupState;

        public override void SetTimeText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
            base.SetTimeText(tm, stringBuilder, displayArg);
            if (EndFrame != null && EndFrame > 0) {
                var lengthInSec = (EndFrame - OccurenceFrame) * DeltaTime;
                stringBuilder.Append($" ({(float)lengthInSec:F2}s)");
            }
        }

        public override void SetDescriptionText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
            // annoyingly powerUP state doesn't reference powerUP asset
            string powerupTranslation;
            if (PowerupState == PowerupState.NoPowerup) {
                powerupTranslation = tm.GetTranslation("ui.generic.none");
            } else {
                powerupTranslation = tm.GetTranslation("coinitem."+PowerupState.ToString().ToLower());
            }
            stringBuilder.Append(tm.GetTranslationWithReplacements(translationPrefix+"powerup.state", "powerup", powerupTranslation));
        }
    }

    public unsafe class PointStarmanChange : TimePoint {
        public PointStarmanChange(ReplayStatsRecorder stats, Frame f, MarioPlayer* mario) : base(stats, f, mario) {}

        public override void SetTimeText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
            base.SetTimeText(tm, stringBuilder, displayArg);
            if (EndFrame != null && EndFrame > 0) {
                var lengthInSec = (EndFrame - OccurenceFrame) * DeltaTime;
                stringBuilder.Append($" ({(float) lengthInSec:F2}s)");
            }
        }

        public override void SetDescriptionText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) => stringBuilder.Append(tm.GetTranslation("ui.replay.stats.entry.powerup.starman"));
    }

    public unsafe class PointReserveChange : TimePoint {
        public readonly PowerupAsset? Powerup;
        public PointReserveChange(ReplayStatsRecorder stats, Frame f, MarioPlayer* mario) : base(stats, f, mario) => Powerup = f.FindAsset(mario->ReserveItem);

        public override void SetTimeText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
            base.SetTimeText(tm, stringBuilder, displayArg);
            if (EndFrame != null && EndFrame > 0) {
                var lengthInSec = (EndFrame - OccurenceFrame) * DeltaTime;
                stringBuilder.Append($" ({(float) lengthInSec:F2}s)");
            }
        }

        public override void SetDescriptionText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
            string powerupTranslation;
            if (Powerup == null) {
                powerupTranslation = tm.GetTranslation("ui.generic.none");
            } else {
                powerupTranslation = tm.GetTranslation(Powerup.TranslationKey);
            }
            stringBuilder.Append(tm.GetTranslationWithReplacements(translationPrefix+"powerup.reserve", "powerup", powerupTranslation));
        }
    }

    public unsafe class PointPowerupCollect : TimePoint {
        public readonly PowerupReserveResult ReserveResult;
        public readonly PowerupAsset Powerup;
        public PointPowerupCollect(ReplayStatsRecorder stats, Frame f, MarioPlayer* mario, PowerupReserveResult reserveResult, PowerupAsset powerupAsset) : base(stats, f, mario) {
            ReserveResult = reserveResult;
            Powerup = powerupAsset;
        }

        public override void SetDescriptionText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
            string translationPrefix2 = translationPrefix+"powerup.grab";
            string powerupTranslation = tm.GetTranslation(Powerup.TranslationKey);
            switch (ReserveResult) {
            case PowerupReserveResult.CollectNewReserveOld:
            case PowerupReserveResult.CollectNewIgnoreOld:
                stringBuilder.Append(tm.GetTranslationWithReplacements($"{translationPrefix2}.collected", "powerup", powerupTranslation));
                break;
            case PowerupReserveResult.KeepOldReserveNew:
                stringBuilder.Append(tm.GetTranslationWithReplacements($"{translationPrefix2}.reserved", "powerup", powerupTranslation));
                break;
            }
        }
    }

    public unsafe class PointBigCollectableSpawned : TimePoint {
        public readonly int AttemptedSpawnCount, SuccessfulSpawnCount, FailedSpawnCount;
        public readonly int Spawnpoints, PositionIndex, UsedSpawns;

        public readonly bool WasBlocked;
        public readonly FPVector2 Coordinates;
        public readonly List<string> BlockingPlayers = new();
        public string? CollectingPlayer; // if a player collected the big star this is their name
        public PointBigCollectableSpawned(ReplayStatsRecorder stats, Frame f, int usedSpawns, int index, bool blocked, FPVector2 coordinates, ref GlobalInfo globalReplayInfo, VersusStageData stage) : base(stats, f) {
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

        public override void SetDescriptionText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
            var translationSuffix = WasBlocked ? "bigcollectableblock" : "bigcollectablespawn";
            //! PositionIndex + 1 since it's zero indexed
            stringBuilder.Append(tm.GetTranslationWithReplacements(translationPrefix + translationSuffix, "position", (PositionIndex+1).ToString(), "spawnpoints", Spawnpoints.ToString()));
        }

        public override void SetAdditionalText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) => stringBuilder.Append(CollectingPlayer);

        public override void SetSymbolsText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
            var blockCol = Color.red;
            var successCol = Color.green;

            stringBuilder.Append("<color=#").Append(Utils.ColorToHex(blockCol, false)).Append('>').Append("X");
            stringBuilder.Append(Utils.GetSymbolString(FailedSpawnCount.ToString(), Utils.smallSymbols, blockCol));
            stringBuilder.Append("<sprite name=\"room_stars\" color=#").Append(Utils.ColorToHex(successCol, false)).Append('>');
            stringBuilder.Append(Utils.GetSymbolString(SuccessfulSpawnCount.ToString(), Utils.smallSymbols, successCol));
        }
    }

    public unsafe class PointBlockHit : TimePoint {
        public readonly bool WasRandom;
        public readonly CoinItemAsset? SpawnedItem;
        public readonly FP? SpawnChancePercentage, SpawnChanceRaw;
        public readonly int CurrStarCount, LeaderStars;
        public readonly FP AverageStarCount;

        public PointBlockHit(Frame f, ReplayStatsRecorder stats, MarioPlayer* mario, bool wasRandom, CoinItemAsset? spawnedItem) : base(stats, f, mario) {
            var gamemode = f.FindAsset(f.Global->Rules.Gamemode);
            WasRandom = wasRandom;
            SpawnedItem = spawnedItem;
            CurrStarCount = gamemode.GetTeamObjectiveCount(f, mario->GetTeam(f)) ?? -1;
            LeaderStars = gamemode.GetFirstPlaceObjectiveCount(f);
            AverageStarCount = gamemode.GetAverageObjectiveCount(f);
            if (WasRandom) {
                SpawnChanceRaw = gamemode.GetItemSpawnWeight(f, spawnedItem, CurrStarCount);
                FP sum = 0;
                foreach (var currCoinItemRef in gamemode.AllCoinItems) {
                    CoinItemAsset currCoinItemAsset = f.FindAsset(currCoinItemRef);

                    var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);

                    if (!currCoinItemAsset.CanSpawn(f, false)) {
                        continue;
                    }
                    sum += gamemode.GetItemSpawnWeight(f, currCoinItemAsset, CurrStarCount);
                }
                SpawnChancePercentage = SpawnChanceRaw / sum * 100;
            }
        }

        public override void SetDescriptionText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
            string translationKey = translationPrefix+"blockhit." + (WasRandom ? "random" : "normal");
            stringBuilder.Append(tm.GetTranslation(translationKey));
        }

        public override string? GetTooltip(TranslationManager tm, DisplayArgs displayArg) {
            if (SpawnedItem == null) {
                return null;
            }
            string translationKey = translationPrefix+"tooltip." + (WasRandom ? "randomspawn" : "itemspawn");
            string itemTranslation = tm.GetTranslation(SpawnedItem.TranslationKey);
            return tm.GetTranslationWithReplacements(translationKey, "item", itemTranslation, "chance", $"{(float) SpawnChancePercentage.GetValueOrDefault():0.00}");
        }
    }
}
