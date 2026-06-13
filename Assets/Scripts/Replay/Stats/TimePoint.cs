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
        //---public variables
        public int EndFrame = -1;
        public virtual bool ShowEndTime => HasEndFrame;
        public virtual bool ShowLength => ShowEndTime;
        public abstract int FrameOffset { get; }

        //---one-set variables
        public readonly PlayerRef AffectedPlayerRef;
        public readonly string AffectedPlayerName;
        public readonly int OccurenceFrame;
        public readonly FP DeltaTime;
        public readonly int Id;
        public readonly int UpdateRate;
        public readonly ReplayStatsRecorder StatsRecorder;

        //---properties (readonly)
        public bool IsGlobalPoint => AffectedPlayerRef == PlayerRef.None;
        public bool HasEndFrame => EndFrame != -1;
        public int Length => !HasEndFrame ? 0 : EndFrame - OccurenceFrame;

        //---static
        private static int _index;
        public const string translationPrefix = "ui.replay.stats.entry.";
        public const int defaultFrameOffset = 30;

        //---enums
        public enum DisplayArgs {
            Normal,
            FromAttacker,
            All
        }

        public int CompareTo(TimePoint other) {
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
        }

        // basic init - per player
        public TimePoint(ReplayStatsRecorder stats, Frame f, MarioPlayer* mario) : this(stats, f) {
            if (mario != null) {
                AffectedPlayerRef = mario->PlayerRef;
                AffectedPlayerName = f.GetPlayerData(mario->PlayerRef).PlayerNickname;
            }
        }

        //---abstractions, overrideables for time point enteries
        public abstract void SetDescriptionText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg);
        public virtual void SetTimeText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
            stringBuilder.Append($"@ {FrameToTime(OccurenceFrame, StatsRecorder.ReplayStart, DeltaTime)}");

            if (HasEndFrame) {
                stringBuilder.Append('-');
            }
            if (ShowEndTime) {
                stringBuilder.Append(FrameToTime(EndFrame, StatsRecorder.ReplayStart, DeltaTime));
            }
            if (ShowLength) {
                var lengthInSec = Length * DeltaTime;
                stringBuilder.Append($" ({(float) lengthInSec:F2}s)");
            }
        }
        public virtual void SetSymbolsText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) { }
        public virtual void SetAdditionalText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
            if (displayArg == DisplayArgs.All) {
                stringBuilder.Append(AffectedPlayerName);
                return;
            }
        }

        public virtual string GetEntryNum(int entryNum, DisplayArgs displayArgs) => entryNum.ToString();
        public virtual string GetTooltip(TranslationManager tm, DisplayArgs displayArg) => null;
        public virtual string GetTooltipLabel(TranslationManager tm, DisplayArgs displayArg) => "!";
        public virtual object GetCameraPos(DisplayArgs displayArg) => AffectedPlayerRef;

        public virtual bool ShowTooltipIcon(TranslationManager tm, DisplayArgs displayArg) => GetTooltip(tm, displayArg) != null;

        //---static methods
        public static void ResetIndex() => _index = 0;

        public static string FrameToTime(Frame f, int initalFrameNum) {
            var seconds = (f.Number - initalFrameNum) * f.DeltaTime;
            var secMod = FPMath.Floor(seconds % 60);
            string time = $"{FPMath.Floor(seconds / 60)}:{FPMath.Floor(secMod / 10) % 10}{secMod % 10}";
            return time;
        }

        public static string FrameToTime(int frameNumber, int initalFrameNum, FP deltaTime) {
            var seconds = (frameNumber - initalFrameNum) * deltaTime;
            var secMod = FPMath.Floor(seconds % 60);
            string time = $"{FPMath.Floor(seconds / 60)}:{FPMath.Floor(secMod / 10) % 10}{secMod % 10}";
            return time;
        }

        // any extra parameters can be GOtten
    }

    public unsafe class PointCoinCollected : TimePoint {
        public override int FrameOffset => 10;
        public readonly CoinItemAsset CoinItem;
        public readonly FP? SpawnChancePercentage, SpawnChanceRaw;
        public readonly int CoinCount, CoinCountTotal, CurrStarCount, LeaderStars;
        public readonly FP AverageStarCount;

        public PointCoinCollected(ReplayStatsRecorder statsRecorder, Frame f, MarioPlayer* mario, PlayerInfo playerInfo, int coinCount, CoinItemAsset coinItemAsset) : base(statsRecorder, f, mario) {
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
                    if (!currCoinItemAsset.CanSpawn(f, false)) {
                        continue;
                    }
                    sum += gamemode.GetItemSpawnWeight(f, currCoinItemAsset, CurrStarCount);
                }
                SpawnChancePercentage = SpawnChanceRaw / sum * 100;
            }
        }

        public override void SetDescriptionText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) => stringBuilder.Append(tm.GetTranslation("ui.replay.stats.entry.coincollected"));

        public override void SetAdditionalText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
            if (CoinItem != null) {
                stringBuilder.Append(tm.GetTranslation(CoinItem.TranslationKey));
            }
        }

        public override void SetSymbolsText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
            if (CoinItem != null) {
                stringBuilder.Append("<sprite name=room_powerups>");
            }

            stringBuilder.Append("<sprite name=room_coins>").Append(Utils.GetSymbolString(CoinCount.ToString(), Utils.smallSymbols));
        }

        public override string GetTooltip(TranslationManager tm, DisplayArgs displayArg) {
            if (CoinItem == null) {
                return null;
            }
            var itemTranslation = tm.GetTranslation(CoinItem.TranslationKey);
            return tm.GetTranslationWithReplacements(translationPrefix+"tooltip.randomspawn", "item", itemTranslation, "chance", $"{(float) SpawnChancePercentage.GetValueOrDefault():0.00}");
        }
    }

    public unsafe class PointDamage : TimePoint {
        public override int FrameOffset => frameOffset;
        public readonly PowerupState NewState;
        public readonly DamageCause Reason;
        public readonly string AttackerName;
        public readonly PlayerRef AttackerRef;
        private readonly int frameOffset;
        public enum DamageCause {
            Enemy,
            Shell,
            Starman,
            MegaMushroom,
            BlueShell,
            Explode,
            Crush
        }

        public PointDamage(ReplayStatsRecorder statsRecorder, Frame f, MarioPlayer* mario, DamageCause reason, string attackerName, PlayerRef attackerRef, int frameOffset) : base(statsRecorder, f, mario) {
            NewState = mario->CurrentPowerupState;
            Reason = reason;
            AttackerName = attackerName;
            AttackerRef = attackerRef;
            this.frameOffset = frameOffset;
        }

        public override void SetDescriptionText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
            string damageOrDealt = displayArg == DisplayArgs.FromAttacker ? "damagedealt." : "damage.";
            stringBuilder.AppendLine(tm.GetTranslation(translationPrefix+damageOrDealt+Reason.ToString().ToLower()));
        }

        public override void SetAdditionalText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
            switch (displayArg) {
            case DisplayArgs.Normal:
                stringBuilder.Append(AttackerName);
                break;
            default:
                stringBuilder.Append(AffectedPlayerName);
                break;
            }
        }

        public override object GetCameraPos(DisplayArgs displayArg) {
            return displayArg switch {
                DisplayArgs.FromAttacker => AttackerRef != PlayerRef.None ? AttackerRef : AffectedPlayerRef,
                _ => AffectedPlayerRef
            };
        }
    }

    public unsafe class PointDeath : TimePoint {
        public override int FrameOffset => frameOffset;
        public readonly int LivesRemaining, Ping;
        public readonly DeathCause Reason;
        public readonly string AttackerName;
        public readonly PlayerRef AttackerRef;
        private readonly int frameOffset;
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
            Disconnect,
            Crush
        }

        public PointDeath(ReplayStatsRecorder statsRecorder, Frame f, MarioPlayer* mario, PlayerInfo playerInfo, DeathCause reason, int ping, string attackerName, PlayerRef attackRef, int frameOffset) : base(statsRecorder, f, mario) {
            LivesRemaining = mario->Lives;
            Reason = reason;
            Ping = ping;
            AttackerName = attackerName;
            AttackerRef = attackRef;
            this.frameOffset = frameOffset;
        }

        public override void SetDescriptionText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
            string deathsOrKills = displayArg == DisplayArgs.FromAttacker ? "kill." : "death.";
            stringBuilder.AppendLine(tm.GetTranslation(translationPrefix+deathsOrKills+Reason.ToString().ToLower()));
        }

        public override void SetSymbolsText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
            if (StatsRecorder != null && StatsRecorder.ReplayFile.Header.Rules.Lives > 0) {
                stringBuilder.Append("<sprite name=room_lives>").Append(Utils.GetSymbolString(LivesRemaining.ToString(), Utils.smallSymbols));
            }
        }

        public override void SetAdditionalText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
            switch (displayArg) {
            case DisplayArgs.Normal:
                stringBuilder.Append(AttackerName);
                break;
            default:
                stringBuilder.Append(AffectedPlayerName);
                break;
            }
        }

        public override string GetTooltip(TranslationManager tm, DisplayArgs displayArg) {
            string translationKeyStart = translationPrefix+"death.tooltip.";
            string tooltip = tm.GetTranslationWithReplacements(translationKeyStart+"ping", "ping", Ping.ToString());

            return tooltip;
        }

        public override object GetCameraPos(DisplayArgs displayArg) {
            return displayArg switch {
                DisplayArgs.FromAttacker => AttackerRef != PlayerRef.None ? AttackerRef : AffectedPlayerRef,
                _ => AffectedPlayerRef
            };
        }
    }

    public unsafe class PointKnockback : TimePoint {
        public override int FrameOffset => defaultFrameOffset;
        public readonly int StarsDropped;
        public readonly string AttackerName;
        public readonly PlayerRef AttackerRef;
        public readonly KnockbackStrength KnockbackStrength;
        
        public bool EndsInDeath;
        public PointKnockback(ReplayStatsRecorder statsRecorder, Frame f, MarioPlayer* mario, EntityRef attacker, int starDropCount, KnockbackStrength knockbackStrength) : base(statsRecorder, f, mario) {
            KnockbackStrength = knockbackStrength;
            StarsDropped = starDropCount;
            if (f.Unsafe.TryGetPointer<MarioPlayer>(attacker, out var attackerMario)) {
                var attackerPlayer = f.GetPlayerData(attackerMario->PlayerRef);
                AttackerRef = attackerMario->PlayerRef;
                AttackerName = attackerPlayer.PlayerNickname;
            }
        }

        public override void SetTimeText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
            if (StatsRecorder == null) {
                stringBuilder.Append("");
                return;
            }
            stringBuilder.Append($"@ {FrameToTime(OccurenceFrame, StatsRecorder.ReplayStart, DeltaTime)}");

            if (HasEndFrame) {
                stringBuilder.Append('-');

                if (ShowLength) {
                    stringBuilder.Append(FrameToTime(EndFrame, StatsRecorder.ReplayStart, DeltaTime));
                    stringBuilder.Append($" ({Length}F)");
                }
            }
        }

        public override void SetDescriptionText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
            string recieveOrDealt = displayArg == DisplayArgs.FromAttacker ? "dealt" : "recieved";
            string translationString = translationPrefix+"knockback." + recieveOrDealt + '.' + KnockbackStrength.ToString().ToLower();
            stringBuilder.Append(tm.GetTranslationWithReplacements(translationString, "victim", AffectedPlayerName));
        }

        public override void SetAdditionalText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
            switch (displayArg) {
            case DisplayArgs.Normal:
                stringBuilder.Append(AttackerName);
                break;
            case DisplayArgs.FromAttacker:
                stringBuilder.Append(AffectedPlayerName);
                break;
            }
        }

        public override void SetSymbolsText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
            var color = Color.red;
            if (EndsInDeath) {
                stringBuilder.Append("<sprite name=\"room_lives\" color=#").Append(Utils.ColorToHex(color, false)).Append('>');
            }
            stringBuilder.Append("<sprite name=\"room_stars\" color=#").Append(Utils.ColorToHex(color, false)).Append('>');
            stringBuilder.Append(Utils.GetSymbolString(StarsDropped.ToString(), Utils.smallSymbols, color: Color.red));
        }

        public override object GetCameraPos(DisplayArgs displayArg) {
            return displayArg switch {
                DisplayArgs.FromAttacker => AttackerRef,
                _ => AffectedPlayerRef
            };
        }
    }

    public unsafe class PointCombo : TimePoint {
        // these are the things that are in the combo
        // we reuse TimePoints for this.

        // tUPle, first is the elemnt, second is stars lost third is total stars lost
        public override int FrameOffset => defaultFrameOffset;
        public readonly List<(TimePoint Element, int StarsLost, int TotalStarsLost)> ComboElements = new();
        public bool GameEnded = false;
        public override bool ShowEndTime => base.ShowEndTime && !GameEnded;


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

        public PointCombo(ReplayStatsRecorder statsRecorder, Frame f, MarioPlayer* mario, TimePoint comboElement, int starsLost) : base(statsRecorder, f, mario) {
            AddComboElement(comboElement, starsLost);
        }

        public void AddComboElement(TimePoint timePoint, int starsLost) {
            int totalStarsLost = starsLost;
            foreach (var element in ComboElements) {
                totalStarsLost += element.StarsLost;
            }
            ComboElements.Add((timePoint, starsLost, totalStarsLost));
        }

        public override void SetTimeText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
            stringBuilder.Append("@ ");
            stringBuilder.Append(string.Join(", ", ComboElements.Select(
                c => FrameToTime(c.Element.OccurenceFrame, StatsRecorder.ReplayStart, c.Element.DeltaTime))
            ));
            stringBuilder.Append($" ({Length}F)");
        }

        public override void SetDescriptionText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
            string translationSuffix = "combo.";
            switch(displayArg) {
            case DisplayArgs.FromAttacker:
                var attackers = GetParticipants();
                translationSuffix += attackers.Count != 1 ? "participated" : "landed";
                break;
            default:
                translationSuffix += "received";
                break;
            }
            stringBuilder.Append(tm.GetTranslationWithReplacements(translationPrefix + translationSuffix, "count", ComboElements.Count.ToString()));
        }

        public override void SetSymbolsText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
            var color = Color.red;
            if (EndsInDeath()) {
                stringBuilder.Append("<sprite name=\"room_lives\" color=#").Append(Utils.ColorToHex(color, false)).Append('>');
            }
            stringBuilder.Append("<sprite name=\"room_stars\" color=#").Append(Utils.ColorToHex(color, false)).Append('>');
            stringBuilder.Append(Utils.GetSymbolString(TotalStarsAfterCombo().ToString(), Utils.smallSymbols, color: Color.red));
        }

        public override void SetAdditionalText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
            switch (displayArg) {
            case DisplayArgs.Normal:
                var attackers = GetParticipants();
                if (attackers.Count > 1) {
                    stringBuilder.Append(tm.GetTranslationWithReplacements(translationPrefix+"combo.participents", "number", attackers.Count.ToString()));
                } else {
                    stringBuilder.Append(attackers.Values.First());
                }
                break;
            case DisplayArgs.FromAttacker:
                stringBuilder.Append(AffectedPlayerName);
                break;
            }
        }

        public override string GetTooltip(TranslationManager tm, DisplayArgs displayArg) {
            StringBuilder sb = new();
            sb.AppendLine(tm.GetTranslation(translationPrefix + "combo.tooltip.parts"));
            foreach (var (Element, _, _) in ComboElements) {
                if (Element is PointKnockback kb) {
                    int frame = kb.OccurenceFrame - OccurenceFrame;
                    bool hasAttacker = kb.AffectedPlayerRef != default;
                    if (hasAttacker) {
                        sb.Append(tm.GetTranslationWithReplacements(translationPrefix + "combo.tooltip.knockback", "attacker", kb.AttackerName, "framenumber", frame.ToString())).AppendLine(" "+kb.StarsDropped+"★");
                    } else {
                        sb.Append(tm.GetTranslationWithReplacements(translationPrefix + "combo.tooltip.knockbacknoattacker", "framenumber", frame.ToString())).AppendLine(" "+kb.StarsDropped+"★");
                    }
                } else if (Element is PointDamage dmg) {
                    dmg.SetDescriptionText(tm, sb, displayArg);
                } else if (Element is PointDeath death) {
                    death.SetDescriptionText(tm, sb, displayArg);
                }
            }

            return sb.ToString();
        }

        public override object GetCameraPos(DisplayArgs displayArg) {
            switch(displayArg) {
            case DisplayArgs.FromAttacker:
                var attackers = GetParticipants();
                if (attackers.Count > 1) {
                    return AffectedPlayerRef;
                } else {
                    return attackers.First().Key;
                }
            default:
                return AffectedPlayerRef;
            }
        }
    }

    public unsafe class PointPowerChange : TimePoint {
        public override int FrameOffset => 10;
        public readonly PowerupState PowerupState;
        public bool GameEnded;
        public override bool ShowEndTime => base.ShowEndTime && !GameEnded;
        public PointPowerChange(ReplayStatsRecorder statsRecorder, Frame f, MarioPlayer* mario) : base(statsRecorder, f, mario) {
            PowerupState = mario->CurrentPowerupState;
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
        public override int FrameOffset => defaultFrameOffset;
        public bool GameEnded;
        public override bool ShowEndTime => base.ShowEndTime && !GameEnded;
        public PointStarmanChange(ReplayStatsRecorder stats, Frame f, MarioPlayer* mario) : base(stats, f, mario) { }
        public override void SetDescriptionText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) => stringBuilder.Append(tm.GetTranslation(translationPrefix+"powerup.starman"));
    }

    public unsafe class PointReserveChange : TimePoint {
        public override int FrameOffset => defaultFrameOffset;
        public readonly PowerupAsset Powerup;
        public bool GameEnded;
        public override bool ShowEndTime => base.ShowEndTime && !GameEnded;
        public PointReserveChange(ReplayStatsRecorder stats, Frame f, MarioPlayer* mario) : base(stats, f, mario) {
            Powerup = f.FindAsset(mario->ReserveItem);
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
        public override int FrameOffset => defaultFrameOffset;
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
        public override int FrameOffset => defaultFrameOffset;
        public readonly int AttemptedSpawnCount, SuccessfulSpawnCount, FailedSpawnCount;
        public readonly int Spawnpoints, PositionIndex, UsedSpawns;

        public readonly bool WasBlocked;
        public readonly FPVector2 Coordinates;
        public readonly List<string> BlockingPlayers;
        public readonly GamemodeAsset GamemodeAsset;
        public PlayerRef CollectingPlayerRef;
        public string CollectingPlayer; // if a player collected the big star this is their name

        public bool GameEnded;
        public override bool ShowEndTime => base.ShowEndTime && !GameEnded;
        public PointBigCollectableSpawned(ReplayStatsRecorder stats, Frame f, int usedSpawns, int index, bool blocked, FPVector2 coordinates, ref GlobalInfo globalReplayInfo, VersusStageData stage, List<string> blockers) : base(stats, f) {
            PositionIndex = index;
            UsedSpawns = usedSpawns;
            WasBlocked = blocked;
            Spawnpoints = stage.BigStarSpawnpoints.Length;
            Coordinates = coordinates;
            GamemodeAsset = f.FindAsset(f.Global->Rules.Gamemode);


            AttemptedSpawnCount = ++globalReplayInfo.AttemptedStarSpawns;
            if (!blocked) {
                ++globalReplayInfo.SuccessfulStarSpawns;
            } else {
                ++globalReplayInfo.FailedStarSpawns;
            }

            SuccessfulSpawnCount = globalReplayInfo.SuccessfulStarSpawns;
            FailedSpawnCount = globalReplayInfo.FailedStarSpawns;
            BlockingPlayers = blockers;
        }

        public override void SetDescriptionText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
            var translationMid = GamemodeAsset is CoinRunnersGamemode ? "starcoin." : "star.";
            var translationSuffix = WasBlocked ? "block" : "spawn";
            //! PositionIndex + 1 since it's zero indexed
            stringBuilder.Append(tm.GetTranslationWithReplacements(translationPrefix + "bigcollectable." + translationMid + translationSuffix, "position", (PositionIndex+1).ToString(), "spawnpoints", Spawnpoints.ToString()));
        }

        public override void SetAdditionalText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
            if (!WasBlocked && CollectingPlayer != null) {
                stringBuilder.Append(CollectingPlayer);
            }
        }

        public override void SetSymbolsText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
            var blockCol = Color.red;
            var successCol = Color.green;

            stringBuilder.Append("<color=#").Append(Utils.ColorToHex(blockCol, false)).Append('>').Append("X");
            stringBuilder.Append(Utils.GetSymbolString(FailedSpawnCount.ToString(), Utils.smallSymbols, color: blockCol));
            stringBuilder.Append("<sprite name=\"room_stars\" color=#").Append(Utils.ColorToHex(successCol, false)).Append('>');
            stringBuilder.Append(Utils.GetSymbolString(SuccessfulSpawnCount.ToString(), Utils.smallSymbols, color: successCol));
        }

        public override string GetTooltip(TranslationManager tm, DisplayArgs displayArg) {
            var translationSuffix = "bigcollectable.tooltip.";

            string spotsRemaining = tm.GetTranslationWithReplacements(translationPrefix+translationSuffix+"remaining", "spawnpoints", (Spawnpoints - UsedSpawns).ToString());
            string blockers = "";
            if (WasBlocked) {
                blockers += '\n' + tm.GetTranslationWithReplacements(translationPrefix+translationSuffix+"blockers", "blockers", string.Join(", ", BlockingPlayers));
            }
            return spotsRemaining + blockers;
        }

        public override object GetCameraPos(DisplayArgs displayArg) => Coordinates.ToUnityVector3();
        public override string GetTooltipLabel(TranslationManager tm, DisplayArgs displayArg) => (Spawnpoints - UsedSpawns).ToString();
    }

    public unsafe class PointBlockHit : TimePoint {
        public override int FrameOffset => defaultFrameOffset;
        public readonly bool WasRandom;
        public readonly CoinItemAsset SpawnedItem;
        public readonly FP SpawnChancePercentage, SpawnChanceRaw;
        public readonly int CurrStarCount, LeaderStars;
        public readonly FP AverageStarCount;

        public PointBlockHit(Frame f, ReplayStatsRecorder stats, MarioPlayer* mario, bool wasRandom, CoinItemAsset spawnedItem) : base(stats, f, mario) {
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
                    if (!currCoinItemAsset.CanSpawn(f, true)) {
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

        public override void SetAdditionalText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
            if (SpawnedItem != null) {
                stringBuilder.Append(tm.GetTranslation(SpawnedItem.TranslationKey));
            }
        }

        public override string GetTooltip(TranslationManager tm, DisplayArgs displayArg) {
            if (SpawnedItem == null) {
                return null;
            }
            string translationKey = translationPrefix+"tooltip." + (WasRandom ? "randomspawn" : "itemspawn");
            string itemTranslation = tm.GetTranslation(SpawnedItem.TranslationKey);
            return tm.GetTranslationWithReplacements(translationKey, "item", itemTranslation, "chance", $"{(float) SpawnChancePercentage:0.00}");
        }
    }

    public unsafe class PointTaunt : TimePoint {
        public override int FrameOffset => defaultFrameOffset;
        public PointTaunt(ReplayStatsRecorder stats, Frame f, MarioPlayer* mario) : base(stats, f, mario) { }
        public override void SetDescriptionText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) {
            string translationKey = translationPrefix+"taunt";
            stringBuilder.Append(tm.GetTranslation(translationKey));
        }
    }

    public unsafe class PointStarCountChange : TimePoint {
        public override int FrameOffset => defaultFrameOffset;
        public int StarCount;
        public bool GameEnded;
        public override bool ShowEndTime => !GameEnded;
        public override bool ShowLength => base.ShowLength && OccurenceFrame != EndFrame;
        public PointStarCountChange(ReplayStatsRecorder statsRecorder, Frame f, MarioPlayer* mario) : base(statsRecorder, f, mario) {
            var gamemode = f.FindAsset(f.Global->Rules.Gamemode);
            StarCount = gamemode.GetObjectiveCount(f, mario);
        }

        public override void SetSymbolsText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) => stringBuilder.Append("<sprite name=room_stars>").Append(Utils.GetSymbolString(StarCount.ToString(), Utils.smallSymbols));

        public override void SetDescriptionText(TranslationManager tm, StringBuilder stringBuilder, DisplayArgs displayArg) => stringBuilder.Append(tm.GetTranslationWithReplacements("ui.replay.stats.entry.starcountchange", "stars", StarCount.ToString()));
    }
}
