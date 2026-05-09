#nullable enable
using Photon.Deterministic;
using Quantum;
using System;
using System.Text;

namespace NSMB.Replay.Stats
{
    public unsafe class EventManager
    {
        public ReplayStatsRecorder StatRecorder;

        public EventManager(ReplayStatsRecorder statRecorder, EventDispatcher eventDispatcher, CallbackDispatcher callbackDispatcher)
        {
            StatRecorder = statRecorder;
            eventDispatcher.Subscribe<EventMarioPlayerCollectedStar>(this, OnMarioPlayerCollectedStar);
            eventDispatcher.Subscribe<EventMarioPlayerDied>(this, OnMarioPlayerDied);
            eventDispatcher.Subscribe<EventMarioPlayerTookKnockback>(this, OnMarioPlayerKnockback);
            eventDispatcher.Subscribe<EventMarioPlayerTookDamage>(this, OnMarioPlayerTookDamage);
            eventDispatcher.Subscribe<EventMarioPlayerCollectedPowerup>(this, OnMarioPlayerCollectedPowerup);
            eventDispatcher.Subscribe<EventMarioPlayerCollectedCoin>(this, OnMarioPlayerCollectedCoin);
            eventDispatcher.Subscribe<EventBigCollectableAttemptedSpawn>(this, OnBigCollectableAttemptedSpawn);

            // callbackDispatcher.Subscribe<CallbackGameStarted>(this, e => OnGameStarted(e.Game.Frames.Predicted));
            callbackDispatcher.Subscribe<CallbackSimulateFinished>(this, e => OnSimulationFinished(e.Frame));
        }

        #region Events

        public void OnMarioPlayerCollectedCoin(EventMarioPlayerCollectedCoin e)
        {
            Frame f = e.Game.Frames.Verified;
            var mario = f.Unsafe.GetPointer<MarioPlayer>(e.Entity);
            var playerInfo = StatRecorder.PlayerInfos[mario->PlayerRef];

            var gamemode = f.FindAsset(f.Global->Rules.Gamemode);
            var firstPlaceObj = gamemode.GetFirstPlaceObjectiveCount(f);
            PointCoinCollected timePoint;
            if (e.ItemSpawned != EntityRef.None)
            {
                int starDifference = firstPlaceObj - gamemode.GetTeamObjectiveCount(f, mario->GetTeam(f)) ?? -1;
                var coinItemPtr = f.Unsafe.GetPointer<CoinItem>(e.ItemSpawned);
                var asset = f.FindAsset(coinItemPtr->Scriptable);
                timePoint = new PointCoinCollected(f, mario, playerInfo, e.Coins, asset);
                UnityEngine.Debug.Log($"{playerInfo.PlayerName} collected {e.Coins} coin(s) ({playerInfo.Coins} total) at {FrameToTime(f, StatRecorder.ReplayStart)} frame {f.Number - StatRecorder.ReplayStart} spawning a {timePoint.ItemName} ({timePoint.SpawnChancePercentage:F2}% chance, dist {starDifference})");
            } else
            {
                timePoint = new PointCoinCollected(f, mario, playerInfo, e.Coins, null);
                UnityEngine.Debug.Log($"{playerInfo.PlayerName} collected coin at {FrameToTime(f, StatRecorder.ReplayStart)} frame {f.Number - StatRecorder.ReplayStart} now has {e.Coins} coins ({playerInfo.Coins} total)");
            }
            playerInfo.CoinsCollectedPoints.Add(timePoint);
        }

        public void OnMarioPlayerCollectedStar(EventMarioPlayerCollectedStar e)
        {
            Frame f = e.Game.Frames.Verified;
            var mario = f.Unsafe.GetPointer<MarioPlayer>(e.Entity);
            var playerInfo = StatRecorder.PlayerInfos[mario->PlayerRef];

            playerInfo.StarsCollectedPoints.Add(new PointStarCollected(f, mario, playerInfo, mario->GamemodeData.StarChasers->Stars));
            UnityEngine.Debug.Log($"{playerInfo.PlayerName} collected star at {FrameToTime(f, StatRecorder.ReplayStart)} frame {f.Number - StatRecorder.ReplayStart} now has {mario->GamemodeData.StarChasers->Stars} stars!");

            // check if the big star is the main one
            if (e.StarEntity == f.Global->MainBigStar && StatRecorder.GlobalInfo.CurrBigCollectable is PointBigCollectableSpawned currBigCollectable)
            {
                currBigCollectable.CollectingPlayer = playerInfo.PlayerName;
                currBigCollectable.EndFrame = f.Number;
                StatRecorder.GlobalInfo.CurrBigCollectable = null;
            }
        }

        public void OnMarioPlayerDied(EventMarioPlayerDied e)
        {
            Frame f = e.Game.Frames.Verified;
            bool livesEnabled = StatRecorder.ReplayFile.Header.Rules.Lives > 0;

            // for some reason when dying via pit the entity and attacker are the same
            bool wasPitDeath = e.Entity == e.Attacker;
            bool wasDisconnect = e.Entity == EntityRef.None;

            var mario = f.Unsafe.GetPointer<MarioPlayer>(e.Entity);
            var playerData = QuantumUtils.GetPlayerData(f, mario->PlayerRef);
            var playerInfo = StatRecorder.PlayerInfos[mario->PlayerRef];
            var deathPoint = new PointDeath(f, mario, playerInfo, playerData->Ping);
            var starsToDrop = Math.Min(1, e.OldObjectiveCount);

            playerInfo.DeathPoints.Add(deathPoint);
            playerInfo.StarsLostPoints.Add(new PointStarLoss(f, mario, playerInfo, starsToDrop, PointStarLoss.StarLossCause.Death, EntityRef.None));

            StringBuilder sb = new();
            sb.Append($"{playerInfo.PlayerName} died at {FrameToTime(f, StatRecorder.ReplayStart)} frame {f.Number - StatRecorder.ReplayStart} with {playerData->Ping}ms ping");
            if (livesEnabled) {
                sb.Append($" now has {mario->Lives} lives...");
            }
            UnityEngine.Debug.Log(sb.ToString());

            // end a combo, finisher
            if (playerInfo.CurrComboPoint != null)
            {
                bool includeInCombo = playerInfo.CurrComboPoint.ComboElements.Count > 1 || !wasPitDeath && !wasDisconnect;
                StatUtilStopCombo(f, playerInfo, StatRecorder, includeInCombo ? deathPoint : null, starsToDrop);
            }
        }

        public void OnMarioPlayerKnockback(EventMarioPlayerTookKnockback e)
        {
            Frame f = e.Game.Frames.Verified;
            KnockbackStrength strength = e.Strength;
            var victimMario = f.Unsafe.GetPointer<MarioPlayer>(e.Entity);
            var victimMarioInfo = StatRecorder.PlayerInfos[victimMario->PlayerRef];

            var attackerMario = f.Unsafe.GetPointer<MarioPlayer>(e.Attacker);
            var attackerMarioInfo = StatRecorder.PlayerInfos[attackerMario->PlayerRef];
           
            bool isProjectile = e.ProjectileEffect != ProjectileEffectType.None;
            bool dropStars = e.StarsToDrop != 0;

            var lossCause = strength switch {
                KnockbackStrength.FireballBump => PointStarLoss.StarLossCause.Fireball,
                KnockbackStrength.CollisionBump => PointStarLoss.StarLossCause.CollisionBump,
                KnockbackStrength.Normal => PointStarLoss.StarLossCause.Stomp,
                KnockbackStrength.Groundpound => PointStarLoss.StarLossCause.HipDrop,
                _ => PointStarLoss.StarLossCause.Unknown
            };

            int starsToDrop = e.StarsToDrop;
            if (dropStars) {
                victimMarioInfo.StarsLostPoints.Add(new PointStarLoss(f, victimMario, victimMarioInfo, starsToDrop, lossCause, e.Attacker));
            }

            // end the current knockback setting the end frame
            if (victimMarioInfo.CurrKnockbackPoint is PointKnockback currKnockbackPoint) {
                currKnockbackPoint.EndFrame = f.Number;
                victimMarioInfo.CurrKnockbackPoint = null;
            }
            var knockbackPoint = new PointKnockback(f, victimMario, e.Attacker, starsToDrop, strength);
            victimMarioInfo.KnockbackPoints.Add(knockbackPoint);
            victimMarioInfo.CurrKnockbackPoint = knockbackPoint;
            StatUtilSetCombo(f, victimMario, victimMarioInfo, knockbackPoint, starsToDrop);

            if (!isProjectile) {
                UnityEngine.Debug.Log($"{victimMarioInfo.PlayerName} recieved {strength} knockback from {attackerMarioInfo.PlayerName} at {FrameToTime(f, StatRecorder.ReplayStart)} frame {f.Number - StatRecorder.ReplayStart}.");
            }
            else {
                UnityEngine.Debug.Log($"{victimMarioInfo.PlayerName} recieved {strength} knockback from {attackerMarioInfo.PlayerName}'s projectile at {FrameToTime(f, StatRecorder.ReplayStart)} frame {f.Number - StatRecorder.ReplayStart}.");
            }
        }

        public void OnMarioPlayerTookDamage(EventMarioPlayerTookDamage e)
        {
            Frame f = e.Game.Frames.Verified;
            var mario = f.Unsafe.GetPointer<MarioPlayer>(e.Entity);
            var marioRuntimeData = f.GetPlayerData(mario->PlayerRef);
            var marioPlayerInfo = StatRecorder.PlayerInfos[mario->PlayerRef];
            var damagePoint = new PointDamage(f, mario);
            var starsToDrop = Math.Min(1, e.OldObjectiveCount);
            marioPlayerInfo.StarsLostPoints.Add(new PointStarLoss(f, mario, marioPlayerInfo, starsToDrop, PointStarLoss.StarLossCause.Damage, EntityRef.None));
            if (marioPlayerInfo.CurrComboPoint != null) {
                StatUtilSetCombo(f, mario, marioPlayerInfo, damagePoint, starsToDrop);
            }
        }

        public void OnMarioPlayerCollectedPowerup(EventMarioPlayerCollectedPowerup e)
        {
            Frame f = e.Game.Frames.Verified;
            var mario = f.Unsafe.GetPointer<MarioPlayer>(e.Entity);
            var marioRuntimeData = f.GetPlayerData(mario->PlayerRef);
            var marioPlayerInfo = StatRecorder.PlayerInfos[mario->PlayerRef];

            var result = e.Result;
            var scriptable = e.Scriptable;

            var point = new PointPowerupCollect(f, mario, result, scriptable);
            marioPlayerInfo.PowerupCollectPoints.Add(point);

            StringBuilder sb = new();
            sb.Append($"{marioRuntimeData.PlayerNickname} ");
            switch (e.Result)
            {
                case PowerupReserveResult.CollectNewIgnoreOld: sb.Append("collected"); break;
                case PowerupReserveResult.CollectNewReserveOld: sb.Append("reserved"); break;
                case PowerupReserveResult.KeepOldReserveNew: sb.Append("discarded"); break;
                default: sb.Append("did something with"); break;
            }
            sb.Append($" a {point.Powerup} at {FrameToTime(f, StatRecorder.ReplayStart)} frame {f.Number - StatRecorder.ReplayStart}.");
            Console.WriteLine(sb.ToString());
        }

        public void OnBigCollectableAttemptedSpawn(EventBigCollectableAttemptedSpawn e)
        {
            Frame f = e.Game.Frames.Predicted;
            int spawnIndex = e.PositionIndex;
            FPVector2 position = e.Position;
            bool wasBlocked = !e.Success;
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            var usedSpawns = f.Global->UsedStarSpawns.GetSetCount();
            var info = StatRecorder.GlobalInfo;
            var point = new PointBigCollectableSpawned(f, usedSpawns, spawnIndex, wasBlocked, position, ref info, stage);
            
            // set the curr big collectable
            if (!wasBlocked)
            {
                info.CurrBigCollectable = point;
                //Console.WriteLine("Set curr big star");
            } else
            {
                var hits = f.Physics2D.OverlapShape(position, 0, f.Context.CircleRadiusTwo, f.Context.PlayerOnlyMask);
                for (int i = 0; i < hits.Count; i++)
                {
                    var hit = hits[i];
                    var marioPtr = f.Unsafe.GetPointer<MarioPlayer>(hit.Entity);
                    var runtimeData = f.GetPlayerData(marioPtr->PlayerRef);
                    point.BlockingPlayers.Add(runtimeData.PlayerNickname);
                }
            }
            info.BigCollectablesSpawned.Add(point);
            var gamemode = f.FindAsset(f.Global->Rules.Gamemode);
            string collectableName;
            if (gamemode is StarChasersGamemode) collectableName = "Big Star";
            else if (gamemode is CoinRunnersGamemode) collectableName = "Star Coin";
            else collectableName = "mrrrrrrow :3";

            StringBuilder sb = new();
            char starSpawnSymbol = wasBlocked ? '☆' : '★';
            sb.Append($"{starSpawnSymbol}A {collectableName} {(wasBlocked ? "blocked" : "spawned")} at {FrameToTime(f, StatRecorder.ReplayFile.Header.InitialFrameNumber)} frame {f.Number - StatRecorder.ReplayFile.Header.InitialFrameNumber} ");
            /*if (wasBlocked) sb.Append($"making {replayInfo.GlobalReplayInfo.FailedStarSpawns} fails");
            else sb.Append($"making {replayInfo.GlobalReplayInfo.SuccessfulStarSpawns} spawns");
            sb.Append($" {replayInfo.GlobalReplayInfo.AttemptedStarSpawns} attempts total");*/
            sb.Append($"- Blocks: {info.FailedStarSpawns}, Spawns: {info.SuccessfulStarSpawns}, Attempts {info.AttemptedStarSpawns} ");
            sb.Append($"- Spots: {point.Spawnpoints - point.UsedSpawns} ");
            sb.Append($"- at position {spawnIndex + 1} {position.ToString()}");
            if (wasBlocked)
            {
                sb.Append(" - blocked by ");
                foreach(var playerName in point.BlockingPlayers)
                {
                    sb.Append(playerName);
                }
            }
            UnityEngine.Debug.Log(sb.ToString());
        }

        #endregion


        #region Simulation Callbacks
        /*public void OnGameStarted(Frame f) {
            // register all the players
            
            UnityEngine.Debug.Log("Hello");
        }*/

        public bool didLoop = false;
        public void OnSimulationFinished(Frame f) {
            // scan all Marios
            var marios = f.Filter<MarioPlayer>();
            while (marios.NextUnsafe(out _, out var marioPlayer)) {
                if (!didLoop) {
                    var runtimeData = f.GetPlayerData(marioPlayer->PlayerRef);
                    StatRecorder.PlayerInfos.Add(marioPlayer->PlayerRef, new PlayerInfo(runtimeData.PlayerNickname));
                }

                // what we're GOing to do is very simple, check if Mario is not in knockback.
                // If he isn't then the combo is over, and we delete the current combo from
                // the list if it has only one element.
                var playerInfo = StatRecorder.PlayerInfos[marioPlayer->PlayerRef];
                HandleCombo(f, marioPlayer, playerInfo);
                HandleStateData(f, marioPlayer, playerInfo);
            }
            didLoop = true;
        }

        private void HandleCombo(Frame f, MarioPlayer* marioPlayer, PlayerInfo playerInfo) {
            /**Knockback Handling**/
            if (!marioPlayer->IsInKnockback) {
                if (playerInfo.ComboEndTimer > 0) {
                    if (--playerInfo.ComboEndTimer == 0) {
                        EventManager.StatUtilStopCombo(f, playerInfo, StatRecorder);
                    }
                }

                // end knockback
                if (playerInfo.CurrKnockbackPoint is PointKnockback currKnockbackPoint) {
                    currKnockbackPoint.EndFrame = f.Number;
                    playerInfo.CurrKnockbackPoint = null;
                }
            }

            // end any remaining combos when game is over
            if (f.Global->GameState == GameState.Ended) {
                EventManager.StatUtilStopCombo(f, playerInfo, StatRecorder, noEndFrame: true);
            }
        }

        private void HandleStateData(Frame f, MarioPlayer* marioPlayer, PlayerInfo playerInfo) {
            /** Current PowerUP State **/
            // power states don't match, create a new time point
            if (playerInfo.CurrPowerChangePoint == null || playerInfo.CurrPowerChangePoint.PowerupState != marioPlayer->CurrentPowerupState) {
                var point = new PointPowerChange(f, marioPlayer);
                playerInfo.PowerChangePoints.Add(point);
                playerInfo.CurrPowerChangePoint = point;
            }

            // set the end frame of the previous point if it exists
            if (playerInfo.CurrPowerChangePoint is PointPowerChange currPoint) {
                currPoint.EndFrame = f.Number;
            }

            /** Current Reserve State **/
            var reservePowerup = f.FindAsset(marioPlayer->ReserveItem);
            if (playerInfo.CurrReserveChangePoint == null || playerInfo.CurrReserveChangePoint.Powerup != reservePowerup) {
                var point = new PointReserveChange(f, marioPlayer);
                playerInfo.ReserveChangePoints.Add(point);
                playerInfo.CurrReserveChangePoint = point;
            }

            // check if the reserve item matches
            if (playerInfo.CurrReserveChangePoint is PointReserveChange currReserveChangePoint) {
                currReserveChangePoint.EndFrame = f.Number;
            }

            /** Current Starman State **/
            if ((!marioPlayer->IsStarmanInvincible || f.Global->GameState == GameState.Ended) && playerInfo.CurrStarmanChangePoint is PointStarmanChange currStarmanChangePoint) {
                // set the end frame
                currStarmanChangePoint.EndFrame = f.Number;
                playerInfo.CurrStarmanChangePoint = null;
            } else if (marioPlayer->IsStarmanInvincible && playerInfo.CurrStarmanChangePoint == null) {
                var point = new PointStarmanChange(f, marioPlayer);
                playerInfo.StarmanChangePoints.Add(point);
                playerInfo.CurrStarmanChangePoint = point;
            }
        }
        #endregion

        #region Static Methods

        public static void StatUtilStopCombo(Frame f, PlayerInfo playerInfo, ReplayStatsRecorder statRecorder, TimePoint? timePoint = null, int starsLost = 0, bool noEndFrame = false) {
            var comb = playerInfo.CurrComboPoint;
            if (comb != null) {
                if (timePoint != null) {
                    // add that element as a finisher
                    comb.ComboElements.Add(timePoint);
                    comb.StarsLost.Add(starsLost);
                    int totalStarsLost = 0;
                    for (int i = 0; i < comb.StarsLost.Count; i++) {
                        totalStarsLost += comb.StarsLost[i];
                    }
                    comb.TotalStarsLost.Add(totalStarsLost);
                }

                // combo not valid, delete
                if (comb.ComboElements.Count < 2) {
                    playerInfo.ComboReceivedPoints.Remove(comb);
                    playerInfo.CurrComboPoint = null;
                    return;
                }

                // length is in frames
                if (!noEndFrame) {
                    comb.EndFrame = f.Number;
                }
                playerInfo.CurrComboPoint = null;
            }
        }

        public static void StatUtilSetCombo(Frame f, MarioPlayer* victimMario, PlayerInfo playerInfo, TimePoint timePoint, int starsLost) {
            // no current combo, make a new one :3
            if (playerInfo.CurrComboPoint == null) {
                var currCombo = new PointCombo(f, victimMario, timePoint, starsLost);
                playerInfo.CurrComboPoint = currCombo;
                playerInfo.ComboReceivedPoints.Add(currCombo);
            } else {
                // add an element to the combo
                playerInfo.CurrComboPoint.ComboElements.Add(timePoint);
                playerInfo.CurrComboPoint.StarsLost.Add(starsLost);
                int totalStarsLost = 0;
                for (int i = 0; i < playerInfo.CurrComboPoint.StarsLost.Count; i++) {
                    totalStarsLost += playerInfo.CurrComboPoint.StarsLost[i];
                }
                playerInfo.CurrComboPoint.TotalStarsLost.Add(totalStarsLost);
            }
            playerInfo.ComboEndTimer = PlayerInfo.ComboTimerStart;
        }

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

        #endregion
    }
}
