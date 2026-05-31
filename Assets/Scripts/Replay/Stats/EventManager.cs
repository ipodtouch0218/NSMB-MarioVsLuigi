#nullable enable
using Photon.Deterministic;
using Quantum;
using System;
using System.Linq;

namespace NSMB.Replay.Stats
{
    public unsafe class EventManager
    {
        public ReplayStatsRecorder StatRecorder;

        public EventManager(ReplayStatsRecorder statRecorder, EventDispatcher eventDispatcher, CallbackDispatcher callbackDispatcher) {
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

        public void OnMarioPlayerCollectedCoin(EventMarioPlayerCollectedCoin e) {
            Frame f = e.Game.Frames.Verified;
            var mario = f.Unsafe.GetPointer<MarioPlayer>(e.Entity);
            var playerInfo = StatRecorder.PlayerInfos[mario->PlayerRef];

            CoinItemAsset? coinItemAsset = null;
            if (e.ItemSpawned != EntityRef.None) {
                var coinItemPtr = f.Unsafe.GetPointer<CoinItem>(e.ItemSpawned);
                coinItemAsset = f.FindAsset(coinItemPtr->Scriptable);
            }
            playerInfo.CoinsCollectedPoints.Add(new PointCoinCollected(StatRecorder, f, mario, playerInfo, e.Coins, coinItemAsset));
        }

        public void OnMarioPlayerCollectedStar(EventMarioPlayerCollectedStar e) {
            Frame f = e.Game.Frames.Verified;
            var mario = f.Unsafe.GetPointer<MarioPlayer>(e.Entity);
            var playerInfo = StatRecorder.PlayerInfos[mario->PlayerRef];
            var gamemode = f.FindAsset(f.Global->Rules.Gamemode);

            playerInfo.StarsCollectedPoints.Add(new PointStarCollected(StatRecorder, f, mario, playerInfo, gamemode.GetObjectiveCount(f, mario)));

            // check if the big star is the main one
            if (e.StarEntity == f.Global->MainBigStar &&
                StatRecorder.GlobalInfo.CurrBigCollectable is PointBigCollectableSpawned currBigCollectable) {
                currBigCollectable.CollectingPlayer = playerInfo.PlayerName;
                currBigCollectable.EndFrame = f.Number;
                StatRecorder.GlobalInfo.CurrBigCollectable = null;
            }
        }

        public void OnMarioPlayerDied(EventMarioPlayerDied e) {
            Frame f = e.Game.Frames.Verified;
            PointDeath.DeathCause deathCause = PointDeath.DeathCause.Enemy;

            // for some reason when dying via pit the entity and attacker are the same
            bool wasPitDeath = e.Entity == e.Attacker;
            bool wasDisconnect = e.Entity == EntityRef.None;

            string? attackerName = null;
            PlayerRef? attackerRef = null;

            var mario = f.Unsafe.GetPointer<MarioPlayer>(e.Entity);
            var playerInfo = StatRecorder.PlayerInfos[mario->PlayerRef];

            if (wasDisconnect) {
                deathCause = PointDeath.DeathCause.Disconnect;
            } else if (wasPitDeath) {
                var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
                var transform = f.Unsafe.GetPointer<Transform2D>(e.Entity);

                // check if above the stage, if so then it was poison
                if (e.IsLava) {
                    deathCause = PointDeath.DeathCause.Lava;
                } else if (transform->Position.Y > stage.StageWorldMin.Y) {
                    deathCause = PointDeath.DeathCause.Poison;
                } else {
                    deathCause = PointDeath.DeathCause.Pit;
                }

                // for finding out the killer
                //! LastAttacker gets cleared...
                /*if (f.Unsafe.TryGetPointer<MarioPlayer>(mario->LastAttacker, out var attackerMario)) {
                    attackerName = f.GetPlayerData(attackerMario->PlayerRef).PlayerNickname;
                    attackerRef = attackerMario->PlayerRef;
                }*/

                if (playerInfo.CurrComboPoint != null) {
                    var lastComboElement = playerInfo.CurrComboPoint.ComboElements.Last();
                    if (lastComboElement.Element is PointKnockback lastKbPoint) {
                        attackerName = lastKbPoint.AttackerName;
                        attackerRef = lastKbPoint.AttackerRef;
                    }
                }
            } else {
                // check if it's a shelled enemy
                if (f.Unsafe.TryGetPointer<Koopa>(e.Attacker, out _)) {
                    f.Unsafe.ComponentGetter<KoopaSystem.Filter>().TryGet(f, e.Attacker, out var koopaFilter);
                    var koopa = koopaFilter.Koopa;
                    var holdable = koopaFilter.Holdable;
                    if (koopa->IsKicked) {
                        deathCause = PointDeath.DeathCause.Shell;
                        if (f.Exists(holdable->PreviousHolder) && koopa->IsKicked) {
                            var holdableMario = f.Unsafe.GetPointer<MarioPlayer>(holdable->PreviousHolder);
                            attackerName = f.GetPlayerData(holdableMario->PlayerRef).PlayerNickname;
                            attackerRef = holdableMario->PlayerRef;
                        }
                    }
                } else if (f.Unsafe.TryGetPointer<Bobomb>(e.Attacker, out _)) {
                    // maybe it's a bobomb...
                    f.Unsafe.ComponentGetter<BobombSystem.Filter>().TryGet(f, e.Attacker, out var bobombFilter);
                    var bobomb = bobombFilter.Bobomb;
                    var holdable = bobombFilter.Holdable;
                    if (bobomb->CurrentDetonationFrames > 0) {
                        if (f.Exists(holdable->PreviousHolder)) {
                            var holdableMario = f.Unsafe.GetPointer<MarioPlayer>(holdable->PreviousHolder);
                            attackerName = f.GetPlayerData(holdableMario->PlayerRef).PlayerNickname;
                            attackerRef = holdableMario->PlayerRef;
                        }
                        deathCause = PointDeath.DeathCause.Explode;
                    }
                } else if (f.Unsafe.TryGetPointer<MarioPlayer>(e.Attacker, out var attackerMario)) {
                    // else it was Mario
                    attackerName = f.GetPlayerData(attackerMario->PlayerRef).PlayerNickname;
                    attackerRef = attackerMario->PlayerRef;

                    if (attackerMario->IsStarmanInvincible) {
                        deathCause = PointDeath.DeathCause.Starman;
                    } else if (attackerMario->CurrentPowerupState == PowerupState.MegaMushroom) {
                        deathCause = PointDeath.DeathCause.MegaMushroom;
                    } else if (attackerMario->IsInShell) {
                        deathCause = PointDeath.DeathCause.BlueShell;
                    }
                } else if (f.Unsafe.TryGetPointer<Enemy>(e.Attacker, out _)) {
                    if (playerInfo.CurrComboPoint != null) {
                        var lastComboElement = playerInfo.CurrComboPoint.ComboElements.Last();
                        if (lastComboElement.Element is PointKnockback lastKbPoint) {
                            attackerName = lastKbPoint.AttackerName;
                            attackerRef = lastKbPoint.AttackerRef;
                        }
                    }
                }
            }

            var playerData = QuantumUtils.GetPlayerData(f, mario->PlayerRef);
            var deathPoint = new PointDeath(StatRecorder, f, mario, playerInfo, deathCause, playerData->Ping, attackerName, attackerRef);
            var starsToDrop = Math.Min(1, e.OldObjectiveCount);

            playerInfo.DeathPoints.Add(deathPoint);
            playerInfo.StarsLostPoints.Add(new PointStarLoss(StatRecorder, f, mario, playerInfo, starsToDrop, PointStarLoss.StarLossCause.Death, EntityRef.None));

            // end a combo as a finisher finisher
            if (playerInfo.CurrComboPoint != null) {
                bool includeInCombo = playerInfo.CurrComboPoint.ComboElements.Count > 1 || !wasPitDeath && !wasDisconnect;
                StatUtilStopCombo(f, playerInfo, includeInCombo ? deathPoint : null, starsToDrop);
            }
        }

        public void OnMarioPlayerKnockback(EventMarioPlayerTookKnockback e) {
            Frame f = e.Game.Frames.Verified;
            KnockbackStrength strength = e.Strength;
            var victimMario = f.Unsafe.GetPointer<MarioPlayer>(e.Entity);
            var victimMarioInfo = StatRecorder.PlayerInfos[victimMario->PlayerRef];

            var attackerMario = f.Unsafe.GetPointer<MarioPlayer>(e.Attacker);
            var attackerMarioInfo = StatRecorder.PlayerInfos[attackerMario->PlayerRef];
           
            //bool isProjectile = e.ProjectileEffect != ProjectileEffectType.None;
            bool dropStars = e.StarsToDrop != 0;

            var lossCause = strength switch {
                KnockbackStrength.FireballBump => PointStarLoss.StarLossCause.Fireball,
                KnockbackStrength.CollisionBump => PointStarLoss.StarLossCause.CollisionBump,
                KnockbackStrength.Normal => PointStarLoss.StarLossCause.Stomp,
                KnockbackStrength.Groundpound => PointStarLoss.StarLossCause.HipDrop,
                _ => PointStarLoss.StarLossCause.Unknown
            };

            int starsToDrop = Math.Min(e.StarsToDrop, e.OldObjectiveCount);
            if (dropStars) {
                victimMarioInfo.StarsLostPoints.Add(new PointStarLoss(StatRecorder, f, victimMario, victimMarioInfo, starsToDrop, lossCause, e.Attacker));
            }

            // end the current knockback setting the end frame
            if (victimMarioInfo.CurrKnockbackPoint is PointKnockback currKnockbackPoint) {
                currKnockbackPoint.EndFrame = f.Number;
            }
            var knockbackPoint = new PointKnockback(StatRecorder, f, victimMario, e.Attacker, starsToDrop, strength);
            victimMarioInfo.KnockbackPoints.Add(knockbackPoint);
            victimMarioInfo.CurrKnockbackPoint = knockbackPoint;
            StatUtilSetCombo(StatRecorder, f, victimMario, victimMarioInfo, knockbackPoint, starsToDrop);
        }

        public void OnMarioPlayerTookDamage(EventMarioPlayerTookDamage e) {
            Frame f = e.Game.Frames.Verified;
            PointDamage.DamageCause damageCause = PointDamage.DamageCause.Enemy;
            var mario = f.Unsafe.GetPointer<MarioPlayer>(e.Entity);
            var marioPlayerInfo = StatRecorder.PlayerInfos[mario->PlayerRef];

            string? attackerName = null;
            PlayerRef? attackerRef = null;

            // check if it's a shelled enemy
            if (f.Unsafe.TryGetPointer<Koopa>(e.Attacker, out _)) {
                f.Unsafe.ComponentGetter<KoopaSystem.Filter>().TryGet(f, e.Attacker, out var koopaFilter);
                var koopa = koopaFilter.Koopa;
                var holdable = koopaFilter.Holdable;
                if (koopa->IsKicked) {
                    damageCause = PointDamage.DamageCause.Shell;
                    if (f.Exists(holdable->PreviousHolder) && koopa->IsKicked) {
                        var holdableMario = f.Unsafe.GetPointer<MarioPlayer>(holdable->PreviousHolder);
                        attackerName = f.GetPlayerData(holdableMario->PlayerRef).PlayerNickname;
                        attackerRef = holdableMario->PlayerRef;
                    }
                }
            } else if (f.Unsafe.TryGetPointer<Bobomb>(e.Attacker, out _)) {
                // maybe it's a bobomb...
                f.Unsafe.ComponentGetter<BobombSystem.Filter>().TryGet(f, e.Attacker, out var bobombFilter);
                var bobomb = bobombFilter.Bobomb;
                var holdable = bobombFilter.Holdable;
                if (bobomb->CurrentDetonationFrames > 0) {
                    if (f.Exists(holdable->PreviousHolder)) {
                        var holdableMario = f.Unsafe.GetPointer<MarioPlayer>(holdable->PreviousHolder);
                        attackerName = f.GetPlayerData(holdableMario->PlayerRef).PlayerNickname;
                        attackerRef = holdableMario->PlayerRef;
                    }
                    damageCause = PointDamage.DamageCause.Explode;
                }
            } else if (f.Unsafe.TryGetPointer<MarioPlayer>(e.Attacker, out var attackerMario)) {
                // else it was Mario
                attackerName = f.GetPlayerData(attackerMario->PlayerRef).PlayerNickname;
                attackerRef = attackerMario->PlayerRef;

                if (attackerMario->IsStarmanInvincible) {
                    damageCause = PointDamage.DamageCause.Starman;
                } else if (attackerMario->CurrentPowerupState == PowerupState.MegaMushroom) {
                    damageCause = PointDamage.DamageCause.MegaMushroom;
                } else if (attackerMario->IsInShell) {
                    damageCause = PointDamage.DamageCause.BlueShell;
                }
            } else if (f.Unsafe.TryGetPointer<Enemy>(e.Attacker, out _)) {
                if (marioPlayerInfo.CurrComboPoint != null) {
                    var lastComboElement = marioPlayerInfo.CurrComboPoint.ComboElements.Last();
                    if (lastComboElement.Element is PointKnockback lastKbPoint) {
                        attackerName = lastKbPoint.AttackerName;
                        attackerRef = lastKbPoint.AttackerRef;
                    }
                }
            }

            var damagePoint = new PointDamage(StatRecorder, f, mario, damageCause, attackerName, attackerRef);

            var starsToDrop = Math.Min(1, e.OldObjectiveCount);
            marioPlayerInfo.StarsLostPoints.Add(new PointStarLoss(StatRecorder, f, mario, marioPlayerInfo, starsToDrop, PointStarLoss.StarLossCause.Damage, EntityRef.None));
            marioPlayerInfo.DamagePoints.Add(damagePoint);
            if (marioPlayerInfo.CurrComboPoint != null) {
                StatUtilSetCombo(StatRecorder, f, mario, marioPlayerInfo, damagePoint, starsToDrop);
            }
        }

        public void OnMarioPlayerCollectedPowerup(EventMarioPlayerCollectedPowerup e) {
            Frame f = e.Game.Frames.Verified;
            var mario = f.Unsafe.GetPointer<MarioPlayer>(e.Entity);
            var marioPlayerInfo = StatRecorder.PlayerInfos[mario->PlayerRef];
            marioPlayerInfo.PowerupCollectPoints.Add(new PointPowerupCollect(StatRecorder, f, mario, e.Result, e.Scriptable));
        }

        public void OnBigCollectableAttemptedSpawn(EventBigCollectableAttemptedSpawn e) {
            Frame f = e.Game.Frames.Predicted;
            FPVector2 position = e.Position;
            bool wasBlocked = !e.Success;
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            var info = StatRecorder.GlobalInfo;
            var point = new PointBigCollectableSpawned(StatRecorder, f, e.UsedSpawnpoints, e.PositionIndex, wasBlocked, position, ref info, stage);
            
            // set the curr big collectable
            if (!wasBlocked) {
                info.CurrBigCollectable = point;
            } else {
                // attempt to find a player overlapping
                var hits = f.Physics2D.OverlapShape(position, 0, f.Context.CircleRadiusTwo, f.Context.PlayerOnlyMask);
                for (int i = 0; i < hits.Count; i++) {
                    var hit = hits[i];
                    var marioPtr = f.Unsafe.GetPointer<MarioPlayer>(hit.Entity);
                    var runtimeData = f.GetPlayerData(marioPtr->PlayerRef);
                    point.BlockingPlayers.Add(runtimeData.PlayerNickname);
                }
            }
            info.BigCollectablesSpawned.Add(point);
        }

        #endregion


        #region Simulation Callbacks
        // buggy
        /*public void OnGameStarted(Frame f) {
            // register all the players
            
            UnityEngine.Debug.Log("Hello");
        }*/

        //! we have to use this janky setUP with a did loop bool since OnGameStarted doesn't work
        private bool didLoop = false;
        public void OnSimulationFinished(Frame f) {
            // scan all Marios
            var marios = f.Filter<MarioPlayer>();
            while (marios.NextUnsafe(out _, out var marioPlayer)) {
                if (!didLoop) {
                    var runtimeData = f.GetPlayerData(marioPlayer->PlayerRef);
                    StatRecorder.PlayerInfos.Add(marioPlayer->PlayerRef, new PlayerInfo(runtimeData.PlayerNickname, marioPlayer->PlayerRef));
                }

                var playerInfo = StatRecorder.PlayerInfos[marioPlayer->PlayerRef];
                HandleCombo(f, marioPlayer, playerInfo);
                HandleStateData(f, marioPlayer, playerInfo);
            }
            didLoop = true;

            if (f.Global->GameState == GameState.Ended && StatRecorder.GlobalInfo.CurrBigCollectable != null) {
                StatRecorder.GlobalInfo.CurrBigCollectable.EndFrame = -1;
            }

            var blockBumps = f.Filter<BlockBump>();
            while (blockBumps.NextUnsafe(out var entityRef, out var blockBump)) {
                var ownerPtr = f.Unsafe.GetPointer<MarioPlayer>(blockBump->Owner);
                var startTileAsset = f.FindAsset(blockBump->StartTile);
                var playerInfo = StatRecorder.PlayerInfos[ownerPtr->PlayerRef];

                if (startTileAsset is PowerupTileBase powerupTile) {
                    if (!playerInfo.BlocksBumped.Contains(entityRef)) {
                        var gamemode = f.FindAsset(f.Global->Rules.Gamemode);
                        CoinItemAsset? coinItemAsset = null;
                        foreach (var coinItem in gamemode.AllCoinItems) {
                            var coinItemAsAsset = f.FindAsset(coinItem);
                            var entityPrototype = f.FindAsset(coinItemAsAsset.Prefab);
                            if (entityPrototype == blockBump->Powerup) {
                                coinItemAsset = coinItemAsAsset;
                                break;
                            }
                        }
                        playerInfo.BlockHitPoints.Add(new PointBlockHit(f, StatRecorder, ownerPtr, powerupTile is RouletteTile, coinItemAsset));
                        playerInfo.BlocksBumped.Add(entityRef);
                    }
                }
            }

            ActiveReplayManager.Instance.TryCacheReplayFrame(f);
        }

        private void HandleCombo(Frame f, MarioPlayer* marioPlayer, PlayerInfo playerInfo) {
            /**Knockback Handling**/
            if (!marioPlayer->IsInKnockback) {
                if (playerInfo.ComboEndTimer > 0) {
                    if (--playerInfo.ComboEndTimer == 0) {
                        EventManager.StatUtilStopCombo(f, playerInfo);
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
                EventManager.StatUtilStopCombo(f, playerInfo, noEndFrame: true);
            }
        }

        private void HandleStateData(Frame f, MarioPlayer* marioPlayer, PlayerInfo playerInfo) {
            bool gameEnded = f.Global->GameState == GameState.Ended;

            /** Current PowerUP State **/
            // power states don't match, create a new time point
            if (playerInfo.CurrPowerChangePoint == null || playerInfo.CurrPowerChangePoint.PowerupState != marioPlayer->CurrentPowerupState) {
                var point = new PointPowerChange(StatRecorder, f, marioPlayer);
                playerInfo.PowerChangePoints.Add(point);
                playerInfo.CurrPowerChangePoint = point;
            }

            // set the end frame of the previous point if it exists
            if (playerInfo.CurrPowerChangePoint is PointPowerChange currPoint) {
                currPoint.EndFrame = gameEnded ? -1 : f.Number;
            }

            /** Current Reserve State **/
            // check if the reserve item matches
            var reservePowerup = f.FindAsset(marioPlayer->ReserveItem);
            if (playerInfo.CurrReserveChangePoint == null || playerInfo.CurrReserveChangePoint.Powerup != reservePowerup) {
                var point = new PointReserveChange(StatRecorder, f, marioPlayer);
                playerInfo.ReserveChangePoints.Add(point);
                playerInfo.CurrReserveChangePoint = point;
            }

            if (playerInfo.CurrReserveChangePoint is PointReserveChange currReserveChangePoint) {
                currReserveChangePoint.EndFrame = gameEnded ? -1 : f.Number;
            }

            /** Current Starman State **/
            if ((!marioPlayer->IsStarmanInvincible || gameEnded) && playerInfo.CurrStarmanChangePoint is PointStarmanChange currStarmanChangePoint) {
                // set the end frame
                currStarmanChangePoint.EndFrame = gameEnded ? -1 : f.Number;
                playerInfo.CurrStarmanChangePoint = null;
            } else if (marioPlayer->IsStarmanInvincible && playerInfo.CurrStarmanChangePoint == null) {
                var point = new PointStarmanChange(StatRecorder, f, marioPlayer);
                playerInfo.StarmanChangePoints.Add(point);
                playerInfo.CurrStarmanChangePoint = point;
            }
        }
        #endregion

        #region Static Methods

        public static void StatUtilStopCombo(Frame f, PlayerInfo playerInfo, TimePoint? timePoint = null, int starsLost = 0, bool noEndFrame = false) {
            var comb = playerInfo.CurrComboPoint;
            if (comb == null) {
                return;
            }

            if (timePoint != null) {
                // add that element as a finisher
                comb.AddComboElement(timePoint, starsLost);
            }

            // combo not valid, delete
            if (comb.ComboElements.Count < 2) {
                playerInfo.ComboReceivedPoints.Remove(comb);
                playerInfo.CurrComboPoint = null;
                return;
            }

            // length is in frames
            comb.EndFrame = noEndFrame ? -1 : f.Number;
            playerInfo.ComboEndTimer = 0;
            playerInfo.CurrComboPoint = null;
        }

        public static void StatUtilSetCombo(ReplayStatsRecorder statsRecorder, Frame f, MarioPlayer* victimMario, PlayerInfo playerInfo, TimePoint timePoint, int starsLost) {
            // no current combo, make a new one :3
            if (playerInfo.CurrComboPoint == null) {
                var currCombo = new PointCombo(statsRecorder, f, victimMario, timePoint, starsLost);
                playerInfo.CurrComboPoint = currCombo;
                playerInfo.ComboReceivedPoints.Add(currCombo);
            } else {
                playerInfo.CurrComboPoint.AddComboElement(timePoint, starsLost);
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
