using Photon.Deterministic;
using Quantum;
using Quantum.Physics2D;
using System;
using System.Collections.Generic;

namespace NSMB.Replay.Stats
{
    public unsafe class EventManager
    {
        public ReplayStatsRecorder StatRecorder;

        public EventManager(ReplayStatsRecorder statRecorder, EventDispatcher eventDispatcher, CallbackDispatcher callbackDispatcher) {
            StatRecorder = statRecorder;

            callbackDispatcher.Subscribe<CallbackGameResynced>(this, e => OnGameStarted(e.Game.Frames.Predicted));
            callbackDispatcher.Subscribe<CallbackSimulateFinished>(this, e => OnSimulationFinished(e.Frame));
        }

        public void HandleEvents(Frame f) {
            while (f.Context.Events.Count > 0) {
                var ev = f.Context.Events.PopHead();
                switch (ev) {
                // player trackers
                case EventMarioPlayerCollectedStar starEvent:
                    OnMarioPlayerCollectedStar(starEvent, f);
                    break;
                case EventMarioPlayerDied diedEvent:
                    OnMarioPlayerDied(diedEvent, f);
                    break;
                case EventMarioPlayerTookKnockback kbEvent:
                    OnMarioPlayerKnockback(kbEvent, f);
                    break;
                case EventMarioPlayerTookDamage damageEvent:
                    OnMarioPlayerTookDamage(damageEvent, f);
                    break;
                case EventMarioPlayerCollectedPowerup powerupEvent:
                    OnMarioPlayerCollectedPowerup(powerupEvent, f);
                    break;
                case EventMarioPlayerCollectedCoin coinEvent:
                    OnMarioPlayerCollectedCoin(coinEvent, f);
                    break;
                case EventMarioPlayerTaunted tauntedEvent:
                    OnMarioPlayerTaunted(tauntedEvent, f);
                    break;

                // global trackers
                case EventBigCollectableAttemptedSpawn bigSpawnEvent:
                    OnBigCollectableAttemptedSpawn(bigSpawnEvent, f);
                    break;
                }
            }
        }

        #region Events

        // player trackers
        public void OnMarioPlayerCollectedCoin(EventMarioPlayerCollectedCoin e, Frame f) {
            var mario = f.Unsafe.GetPointer<MarioPlayer>(e.Entity);

            if (mario->PlayerRef == default) {
                return;
            }

            var playerInfo = StatRecorder.PlayerInfos[mario->PlayerRef];

            CoinItemAsset coinItemAsset = null;
            if (e.ItemSpawned != EntityRef.None) {
                var coinItemPtr = f.Unsafe.GetPointer<CoinItem>(e.ItemSpawned);
                coinItemAsset = f.FindAsset(coinItemPtr->Scriptable);
            }
            playerInfo.CoinsCollectedPoints.Add(new PointCoinCollected(StatRecorder, f, mario, playerInfo, e.Coins, coinItemAsset));
        }

        public void OnMarioPlayerCollectedStar(EventMarioPlayerCollectedStar e, Frame f) {
            var mario = f.Unsafe.GetPointer<MarioPlayer>(e.Entity);
            
            // sanity check, somehow this can occur
            if (mario->PlayerRef == default) {
                return;
            }

            var playerInfo = StatRecorder.PlayerInfos[mario->PlayerRef];
            var gamemode = f.FindAsset(f.Global->Rules.Gamemode);

            //playerInfo.StarsCollectedPoints.Add(new PointStarCollected(StatRecorder, f, mario, playerInfo, gamemode.GetObjectiveCount(f, mario)));

            // check if the big star is the main one
            if (e.StarEntity == f.Global->MainBigStar &&
                StatRecorder.GlobalInfo.CurrBigCollectable is PointBigCollectableSpawned currBigCollectable) {
                currBigCollectable.CollectingPlayer = playerInfo.PlayerName;
                currBigCollectable.EndFrame = f.Number;
                currBigCollectable.CollectingPlayerRef = mario->PlayerRef;
                StatRecorder.GlobalInfo.CurrBigCollectable = null;
            }
        }

        public void OnMarioPlayerDied(EventMarioPlayerDied e, Frame f) {
            var mario = f.Unsafe.GetPointer<MarioPlayer>(e.Entity);

            // sanity check, we will have a null PlayerRef if a player disconnects
            if (mario->PlayerRef == default) {
                return;
            }

            PointDeath.DeathCause deathCause = PointDeath.DeathCause.Enemy;

            // for some reason when dying via pit the entity and attacker are the same
            bool wasPitDeath = e.Entity == e.Attacker;
            bool wasDisconnect = e.Entity == EntityRef.None;

            string attackerName = "";
            PlayerRef attackerRef = default;

            var playerInfo = StatRecorder.PlayerInfos[mario->PlayerRef];
            var lastKbPoint = playerInfo.LastKnockbackPointForDeath;
            int startFrameOffset = 45;

            void SetDetailsFromKb() {
                if (lastKbPoint != null) {
                    attackerName = lastKbPoint.AttackerName;
                    attackerRef = lastKbPoint.AttackerRef;
                    var lastComboPoint = playerInfo.LastComboPointForDeath;
                    if (lastComboPoint != null) {
                        startFrameOffset = f.Number - lastComboPoint.OccurenceFrame + TimePoint.defaultFrameOffset;
                    } else {
                        startFrameOffset = f.Number - lastKbPoint.OccurenceFrame + TimePoint.defaultFrameOffset;
                    }
                }
            }

            void SetDetailsFromHoldable(Holdable* holdable) {
                if (f.Exists(holdable->PreviousHolder)) {
                    var holdableMario = f.Unsafe.GetPointer<MarioPlayer>(holdable->PreviousHolder);
                    attackerName = f.GetPlayerData(holdableMario->PlayerRef).PlayerNickname;
                    attackerRef = holdableMario->PlayerRef;
                }
            }

            if (wasDisconnect) {
                deathCause = PointDeath.DeathCause.Disconnect;
            } else if (wasPitDeath) {
                var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
                var transform = f.Unsafe.GetPointer<Transform2D>(e.Entity);

                // check if above the stage, if so then it was poison
                if (e.IsLava) {
                    deathCause = PointDeath.DeathCause.Lava;
                } else if (transform->Position.Y > stage.StageWorldMin.Y) {
                    var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(e.Entity);
                    deathCause = physicsObject->WasBeingCrushed ? PointDeath.DeathCause.Crush : PointDeath.DeathCause.Poison;
                } else {
                    deathCause = PointDeath.DeathCause.Pit;
                }
                SetDetailsFromKb();
            } else {
                // check if it's a shelled enemy
                if (f.Unsafe.TryGetPointer<Koopa>(e.Attacker, out _)) {
                    f.Unsafe.ComponentGetter<KoopaSystem.Filter>().TryGet(f, e.Attacker, out var koopaFilter);
                    if (koopaFilter.Koopa->IsKicked) {
                        deathCause = PointDeath.DeathCause.Shell;
                        SetDetailsFromHoldable(koopaFilter.Holdable);
                    } else {
                        SetDetailsFromKb();
                    }
                } else if (f.Unsafe.TryGetPointer<Bobomb>(e.Attacker, out _)) {
                    // maybe it's a bobomb...
                    f.Unsafe.ComponentGetter<BobombSystem.Filter>().TryGet(f, e.Attacker, out var bobombFilter);
                    if (bobombFilter.Bobomb->CurrentDetonationFrames <= 0) {
                        startFrameOffset = bobombFilter.Bobomb->DetonationFrames + TimePoint.defaultFrameOffset;
                        SetDetailsFromHoldable(bobombFilter.Holdable);
                        deathCause = PointDeath.DeathCause.Explode;
                    } else {
                        SetDetailsFromKb();
                    }
                } else if (f.Unsafe.TryGetPointer<MarioPlayer>(e.Attacker, out var attackerMario)) {
                    // else it was Mario
                    startFrameOffset = 60;
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
                    SetDetailsFromKb();
                }
            }

            var playerData = QuantumUtils.GetPlayerData(f, mario->PlayerRef);
            var deathPoint = new PointDeath(StatRecorder, f, mario, playerInfo, deathCause, playerData->Ping, attackerName, attackerRef, startFrameOffset);
            var starsToDrop = Math.Min(1, e.OldObjectiveCount);

            playerInfo.DeathPoints.Add(deathPoint);

            if (lastKbPoint != null) {
                lastKbPoint.EndsInDeath = true;
            }

            // end a combo as a finisher finisher
            if (playerInfo.CurrComboPoint != null) {
                // that knockback ended in death
                bool includeInCombo = playerInfo.CurrComboPoint.ComboElements.Count > 1 || !wasPitDeath && !wasDisconnect;
                StopCombo(f, playerInfo, includeInCombo ? deathPoint : null, starsToDrop);
            }
        }

        public void OnMarioPlayerTookDamage(EventMarioPlayerTookDamage e, Frame f) {
            PointDamage.DamageCause damageCause = PointDamage.DamageCause.Enemy;
            var mario = f.Unsafe.GetPointer<MarioPlayer>(e.Entity);
            if (mario->PlayerRef == default) {
                return;
            }

            var marioPlayerInfo = StatRecorder.PlayerInfos[mario->PlayerRef];
            var lastKbPoint = marioPlayerInfo.LastKnockbackPointForDeath;
            int startFrameOffset = 45;

            string attackerName = "";
            PlayerRef attackerRef = default;

            void SetDetailsFromKb() {
                if (lastKbPoint != null) {
                    attackerName = lastKbPoint.AttackerName;
                    attackerRef = lastKbPoint.AttackerRef;
                    var lastComboPoint = marioPlayerInfo.LastComboPointForDeath;
                    if (lastComboPoint != null) {
                        startFrameOffset = f.Number - lastComboPoint.OccurenceFrame + TimePoint.defaultFrameOffset;
                    } else {
                        startFrameOffset = f.Number - lastKbPoint.OccurenceFrame + TimePoint.defaultFrameOffset;
                    }
                }
            }

            void SetDetailsFromHoldable(Holdable* holdable) {
                if (f.Exists(holdable->PreviousHolder)) {
                    var holdableMario = f.Unsafe.GetPointer<MarioPlayer>(holdable->PreviousHolder);
                    attackerName = f.GetPlayerData(holdableMario->PlayerRef).PlayerNickname;
                    attackerRef = holdableMario->PlayerRef;
                }
            }

            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(e.Entity);

            // check if it's a shelled enemy
            if (physicsObject->IsBeingCrushed) {
                damageCause = PointDamage.DamageCause.Crush;
                SetDetailsFromKb();
            } else if (f.Unsafe.TryGetPointer<Koopa>(e.Attacker, out _)) {
                f.Unsafe.ComponentGetter<KoopaSystem.Filter>().TryGet(f, e.Attacker, out var koopaFilter);
                if (koopaFilter.Koopa->IsKicked) {
                    damageCause = PointDamage.DamageCause.Shell;
                    SetDetailsFromHoldable(koopaFilter.Holdable);
                } else {
                    SetDetailsFromKb();
                }
            } else if (f.Unsafe.TryGetPointer<Bobomb>(e.Attacker, out _)) {
                // maybe it's a bobomb...
                f.Unsafe.ComponentGetter<BobombSystem.Filter>().TryGet(f, e.Attacker, out var bobombFilter);
                if (bobombFilter.Bobomb->CurrentDetonationFrames > 0) {
                    startFrameOffset = bobombFilter.Bobomb->DetonationFrames + TimePoint.defaultFrameOffset;
                    SetDetailsFromHoldable(bobombFilter.Holdable);
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
                SetDetailsFromKb();
            }

            var damagePoint = new PointDamage(StatRecorder, f, mario, damageCause, attackerName, attackerRef, startFrameOffset);

            var starsToDrop = Math.Min(1, e.OldObjectiveCount);
            marioPlayerInfo.DamagePoints.Add(damagePoint);
            if (marioPlayerInfo.CurrComboPoint != null) {
                StartOrUpdateCombo(StatRecorder, f, mario, marioPlayerInfo, damagePoint, starsToDrop);
            }
        }

        public void OnMarioPlayerKnockback(EventMarioPlayerTookKnockback e, Frame f) {
            var victimMario = f.Unsafe.GetPointer<MarioPlayer>(e.Entity);
            if (victimMario->PlayerRef == default) {
                return;
            }


            KnockbackStrength strength = e.Strength;
            var victimMarioInfo = StatRecorder.PlayerInfos[victimMario->PlayerRef];
            PlayerInfo attackerMarioInfo = null;

            if (f.Unsafe.TryGetPointer<MarioPlayer>(e.Attacker, out var attackerMario)) {
                if (attackerMario->PlayerRef != default) {
                    attackerMarioInfo = StatRecorder.PlayerInfos[attackerMario->PlayerRef];
                }
            }


            //bool isProjectile = e.ProjectileEffect != ProjectileEffectType.None;
            bool dropStars = e.StarsToDrop != 0;
            int starsToDrop = Math.Min(e.StarsToDrop, e.OldObjectiveCount);

            // end the current knockback setting the end frame
            if (victimMarioInfo.CurrKnockbackPoint is PointKnockback currKnockbackPoint) {
                currKnockbackPoint.EndFrame = f.Number;
            }
            var knockbackPoint = new PointKnockback(StatRecorder, f, victimMario, e.Attacker, starsToDrop, strength);
            victimMarioInfo.KnockbackPoints.Add(knockbackPoint);
            victimMarioInfo.CurrKnockbackPoint = knockbackPoint;
            victimMarioInfo.LastKnockbackPointForDeath = knockbackPoint;
            StartOrUpdateCombo(StatRecorder, f, victimMario, victimMarioInfo, knockbackPoint, starsToDrop);
        }

        public void OnMarioPlayerCollectedPowerup(EventMarioPlayerCollectedPowerup e, Frame f) {
            var mario = f.Unsafe.GetPointer<MarioPlayer>(e.Entity);
            if (mario->PlayerRef == default) {
                return;
            }

            var marioPlayerInfo = StatRecorder.PlayerInfos[mario->PlayerRef];
            marioPlayerInfo.PowerupCollectPoints.Add(new PointPowerupCollect(StatRecorder, f, mario, e.Result, e.Scriptable));
        }


        public void OnMarioPlayerTaunted(EventMarioPlayerTaunted e, Frame f) {
            var mario = f.Unsafe.GetPointer<MarioPlayer>(e.Entity);
            if (mario->PlayerRef == default) {
                return;
            }

            var marioPlayerInfo = StatRecorder.PlayerInfos[mario->PlayerRef];
            marioPlayerInfo.TauntPoints.Add(new PointTaunt(StatRecorder, f, mario));
        }

        // global trackers
        public void OnBigCollectableAttemptedSpawn(EventBigCollectableAttemptedSpawn e, Frame f) {
            FPVector2 position = e.Position;
            bool wasBlocked = !e.Success;
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            var info = StatRecorder.GlobalInfo;
            List<string> blockers = null;

            if (wasBlocked) {
                blockers = new();
                HitCollection hits = f.Physics2D.OverlapShape(position, 0, f.Context.CircleRadiusTwo, f.Context.PlayerOnlyMask);;
                for (int i = 0; i < hits.Count; i++) {
                    var hit = hits[i];
                    var mario = f.Unsafe.GetPointer<MarioPlayer>(hit.Entity);
                    var runtimeData = f.GetPlayerData(mario->PlayerRef);
                    blockers.Add(runtimeData.PlayerNickname);
                }
                
            }

            var point = new PointBigCollectableSpawned(StatRecorder, f, e.UsedSpawnpoints, e.PositionIndex, wasBlocked, position, ref info, stage, blockers);

            // set the curr big collectable
            if (!wasBlocked) {
                info.CurrBigCollectable = point;
            }
            info.BigCollectablesSpawned.Add(point);
        }

        #endregion


        #region Simulation Callbacks
        public void OnGameStarted(Frame f) {
            // register all the players
            foreach ((_, var data) in f.Unsafe.GetComponentBlockIterator<PlayerData>()) {
                var runtimeData = f.GetPlayerData(data->PlayerRef);
                StatRecorder.PlayerInfos[data->PlayerRef] = new PlayerInfo(runtimeData.PlayerNickname, data->PlayerRef);
            }
        }

        public void OnSimulationFinished(Frame f) {
            // scan all Marios
            var marios = f.Filter<MarioPlayer>();
            while (marios.NextUnsafe(out var marioEntity, out var marioPlayer)) {
                // sanity check, player who disconnects has null player ref
                if (marioPlayer->PlayerRef == default) {
                    continue;
                }

                bool livesEnabled = f.Global->Rules.IsLivesEnabled;
                bool outOfLives = livesEnabled && marioPlayer->Lives < 1;

                var playerInfo = StatRecorder.PlayerInfos[marioPlayer->PlayerRef];
                HandleCombo(f, marioPlayer, marioEntity, playerInfo);
                if (!outOfLives) {
                    HandleChangeData(f, marioPlayer, playerInfo);
                }
            }

            if (StatRecorder.GlobalInfo.CurrBigCollectable is PointBigCollectableSpawned currBigCollectable) {
                currBigCollectable.EndFrame = f.Number;
                currBigCollectable.GameEnded = f.Global->GameState == GameState.Ended;
            }

            var blockBumps = f.Filter<BlockBump>();
            while (blockBumps.NextUnsafe(out var entityRef, out var blockBump)) {
                var ownerPtr = f.Unsafe.GetPointer<MarioPlayer>(blockBump->Owner);
                var startTileAsset = f.FindAsset(blockBump->StartTile);
                var playerInfo = StatRecorder.PlayerInfos[ownerPtr->PlayerRef];

                if (startTileAsset is PowerupTileBase powerupTile) {
                    if (!playerInfo.BlocksBumped.Contains(entityRef)) {
                        var gamemode = f.FindAsset(f.Global->Rules.Gamemode);
                        CoinItemAsset coinItemAsset = null;
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

            // handle queued events
            // eventDispatcher.Subscribe happens on an UPdate frame
            HandleEvents(f);
            if (f.Number - StatRecorder.ReplayStart >= 5 * f.UpdateRate) {
                ActiveReplayManager.Instance.TryCacheReplayFrame(f);
            }
        }

        private void HandleCombo(Frame f, MarioPlayer* marioPlayer, EntityRef marioEntity, PlayerInfo playerInfo) {
            /**Knockback Handling**/
            if (playerInfo.CurrKnockbackPoint is PointKnockback currKnockbackPoint) {
                currKnockbackPoint.EndFrame = f.Number;
            }
            
            if (!marioPlayer->IsInKnockback) {
                if (playerInfo.CurrComboPoint != null && --playerInfo.ComboEndTimer <= 0) {
                    StopCombo(f, playerInfo);
                }

                // end knockback
                playerInfo.CurrKnockbackPoint = null;

                // check if Mario's touching the ground
                if (playerInfo.LastKnockbackPointForDeath != null) {
                    var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);
                    if (physicsObject->IsTouchingGround) {
                        playerInfo.LastKnockbackPointForDeath = null;
                        playerInfo.LastComboPointForDeath = null;
                    }
                }
            }

            // end any remaining combos when game is over
            if (f.Global->GameState == GameState.Ended) {
                StopCombo(f, playerInfo, gameEnded: true);
            }
        }

        private void HandleChangeData(Frame f, MarioPlayer* marioPlayer, PlayerInfo playerInfo) {
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
                currPoint.EndFrame = f.Number;
                currPoint.GameEnded = gameEnded;
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
                currReserveChangePoint.EndFrame = f.Number;
                currReserveChangePoint.GameEnded = gameEnded;
            }

            /** Current Star Count State **/
            var gamemode = f.FindAsset(f.Global->Rules.Gamemode);
            if (playerInfo.CurrStarCountChangePoint == null || gamemode.GetObjectiveCount(f, marioPlayer) != playerInfo.CurrStarCountChangePoint.StarCount) {
                var point = new PointStarCountChange(StatRecorder, f, marioPlayer);
                playerInfo.StarCountChangePoints.Add(point);
                playerInfo.CurrStarCountChangePoint = point;
            }
            if (playerInfo.CurrStarCountChangePoint is PointStarCountChange currStarCountChangePoint) {
                currStarCountChangePoint.EndFrame = f.Number;
                currStarCountChangePoint.GameEnded = gameEnded;
            }

            /** Current Starman State **/
            if ((!marioPlayer->IsStarmanInvincible || gameEnded) && playerInfo.CurrStarmanChangePoint is PointStarmanChange currStarmanChangePoint) {
                // set the end frame
                currStarmanChangePoint.EndFrame = f.Number;
                currStarmanChangePoint.GameEnded = gameEnded;
                playerInfo.CurrStarmanChangePoint = null;
            } else if (marioPlayer->IsStarmanInvincible && playerInfo.CurrStarmanChangePoint == null) {
                var point = new PointStarmanChange(StatRecorder, f, marioPlayer);
                playerInfo.StarmanChangePoints.Add(point);
                playerInfo.CurrStarmanChangePoint = point;
            }
        }
        #endregion

        #region Static Methods

        public static void StopCombo(Frame f, PlayerInfo playerInfo, TimePoint timePoint = null, int starsLost = 0, bool gameEnded = false) {
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
                playerInfo.LastComboPointForDeath = null;
                return;
            }

            // length is in frames
            comb.EndFrame = f.Number;
            comb.GameEnded = gameEnded;
            playerInfo.ComboEndTimer = 0;
            playerInfo.CurrComboPoint = null;
        }

        public static void StartOrUpdateCombo(ReplayStatsRecorder statsRecorder, Frame f, MarioPlayer* victimMario, PlayerInfo playerInfo, TimePoint timePoint, int starsLost) {
            // no current combo, make a new one :3
            if (playerInfo.CurrComboPoint == null) {
                var currCombo = new PointCombo(statsRecorder, f, victimMario, timePoint, starsLost);
                playerInfo.CurrComboPoint = currCombo;
                playerInfo.LastComboPointForDeath = currCombo;
                playerInfo.ComboReceivedPoints.Add(currCombo);
            } else {
                playerInfo.CurrComboPoint.AddComboElement(timePoint, starsLost);
            }
            playerInfo.ComboEndTimer = PlayerInfo.ComboTimerStart;
        }

        #endregion
    }
}
