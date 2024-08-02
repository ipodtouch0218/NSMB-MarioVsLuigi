using Photon.Deterministic;
using Quantum.Collections;
using Quantum.Physics2D;
using UnityEngine;
using static IInteractableTile;

namespace Quantum {

    public unsafe class KoopaSystem : SystemMainThreadFilterStage<KoopaSystem.Filter>, ISignalOnThrowHoldable, ISignalOnEnemyRespawned, ISignalOnEntityBumped {
        public struct Filter {
            public EntityRef Entity;
            public Enemy* Enemy;
            public Koopa* Koopa;
            public Transform2D* Transform;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* Collider;
            public Holdable* Holdable;
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var enemy = filter.Enemy;
            var koopa = filter.Koopa;
            var transform = filter.Transform;
            var physicsObject = filter.PhysicsObject;

            // Inactive check
            if (!enemy->IsAlive) {
                return;
            }

            QuantumUtils.Decrement(ref koopa->IgnoreOwnerFrames);
            if (koopa->IsInShell && !koopa->IsKicked) {
                if (QuantumUtils.Decrement(ref koopa->WakeupFrames)) {
                    koopa->IsInShell = false;
                    koopa->IsKicked = false;
                    koopa->IsFlipped = false;
                    koopa->CurrentSpeed = koopa->Speed;
                    enemy->FacingRight = false;

                    var holdable = filter.Holdable;
                    if (f.Exists(holdable->Holder)) {
                        var mario = f.Unsafe.GetPointer<MarioPlayer>(holdable->Holder);
                        mario->HeldEntity = default;
                        holdable->PreviousHolder = default;
                        holdable->Holder = default;
                    }
                }
            }

            // Turn around when hitting a wall.
            if (physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall) {
                enemy->FacingRight = physicsObject->IsTouchingLeftWall;

                if (koopa->IsKicked) {
                    bool? playBumpSound = null;
                    QList<PhysicsContact> contacts = f.ResolveList(physicsObject->Contacts);
                    foreach (var contact in contacts) {
                        FP dot = FPVector2.Dot(contact.Normal, FPVector2.Right);
                        bool right = dot < 0;
                        if (FPMath.Abs(dot) < FP._0_75) {
                            continue;
                        }

                        // Floor tiles.
                        var tileInstance = stage.GetTileRelative(f, contact.TileX, contact.TileY);
                        StageTile tile = f.FindAsset(tileInstance.Tile);
                        if (tile is IInteractableTile it) {
                            it.Interact(f, filter.Entity, right ? InteractionDirection.Right : InteractionDirection.Left,
                                new Vector2Int(contact.TileX, contact.TileY), tileInstance, out bool tempPlayBumpSound);

                            playBumpSound &= (playBumpSound ?? true) & tempPlayBumpSound;
                        }
                    }

                    if (playBumpSound ?? true) {
                        f.Events.PlayBumpSound(f, filter.Entity);
                    }
                }
            }

            // Move
            if (koopa->IsKicked
                || !koopa->IsInShell
                || physicsObject->IsTouchingLeftWall
                || physicsObject->IsTouchingRightWall
                || physicsObject->IsTouchingGround) {

                physicsObject->Velocity.X = koopa->CurrentSpeed * (enemy->FacingRight ? 1 : -1);
            }

            if (koopa->DontWalkOfLedges && !koopa->IsInShell && physicsObject->IsTouchingGround) {
                FPVector2 checkPosition = transform->Position + (FPVector2.Up * FP._0_05) + (FPVector2.Right * FP._0_10 * (enemy->FacingRight ? 1 : -1));
                if (!PhysicsObjectSystem.Raycast(f, stage, checkPosition, FPVector2.Down, FP._0_50, out var hit)) {
                    enemy->FacingRight = !enemy->FacingRight;
                }
            }

            // Collide
            var hits = f.Physics2D.OverlapShape(*transform, filter.Collider->Shape);
            for (int i = 0; i < hits.Count; i++) {
                OnCollision(f, ref filter, hits[i], stage);
            }
        }

        public void OnCollision(Frame f, ref Filter filter, Hit hit, VersusStageData stage) {
            if (hit.Entity == filter.Entity) {
                return;
            }
            var enemy = filter.Enemy;
            var koopa = filter.Koopa;
            var koopaTransform = filter.Transform;
            var collider = filter.Collider;
            var holdable = filter.Holdable;

            if (holdable->Holder == hit.Entity || (holdable->PreviousHolder == hit.Entity && koopa->IgnoreOwnerFrames > 0)) {
                return;
            }

            bool beingHeld = f.Exists(holdable->Holder);

            if (f.Unsafe.TryGetPointer(hit.Entity, out MarioPlayer* mario)
                && f.Unsafe.TryGetPointer(hit.Entity, out Transform2D* marioTransform)
                && f.Unsafe.TryGetPointer(hit.Entity, out PhysicsObject* marioPhysicsObject)) {

                if (mario->IsDead || beingHeld) {
                    return;
                }

                // Mario touched an alive koopa.
                QuantumUtils.UnwrapWorldLocations(stage, koopaTransform->Position + FPVector2.Up * FP._0_10, marioTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
                FPVector2 damageDirection = (theirPos - ourPos).Normalized;
                bool attackedFromAbove = FPVector2.Dot(damageDirection, FPVector2.Up) > FP._0_25;

                if (mario->InstakillsEnemies(*marioPhysicsObject)) {
                    koopa->Kill(f, filter.Entity, hit.Entity, true);
                    return;
                }

                bool groundpounded = attackedFromAbove && mario->IsGroundpoundActive && mario->CurrentPowerupState != PowerupState.MiniMushroom;
                if (groundpounded) {
                    if (koopa->SpawnPowerupWhenStomped.IsValid && f.TryFindAsset(koopa->SpawnPowerupWhenStomped, out PowerupAsset powerup)) {
                        // Powerup (for blue koopa): give to mario immediately
                        PowerupSystem.CollectPowerup(f, hit.Entity, mario, marioPhysicsObject, powerup);
                        enemy->IsActive = false;
                        enemy->IsDead = true;

                    } else if (!koopa->IsSpiny || koopa->IsInShell) {
                        // Kick
                        koopa->IsInShell = true;
                        koopa->IsKicked = false;
                        koopa->EnterShell(f, filter.Entity, hit.Entity, false);
                        koopa->Kick(f, filter.Entity, hit.Entity, 3);
                        filter.PhysicsObject->Velocity.Y = 2;
                    } else {
                        // Groundpounded a Spiny, damage player.
                        if (mario->IsDamageable) {
                            mario->Powerdown(f, hit.Entity, false);
                            if (!koopa->IsInShell) {
                                enemy->FacingRight = damageDirection.X > 0;
                            }
                        }
                    }
                } else if (koopa->IsKicked || !koopa->IsInShell) {
                    // Moving (either in shell, or walking)
                    if (attackedFromAbove) {
                        if (koopa->IsInShell || !koopa->IsSpiny) {
                            // Enter Shell
                            if (koopa->SpawnPowerupWhenStomped.IsValid) {
                                PowerupAsset powerupAsset = f.FindAsset(koopa->SpawnPowerupWhenStomped);
                                EntityRef powerupEntity = f.Create(powerupAsset.Prefab);
                                var powerupTransform = f.Unsafe.GetPointer<Transform2D>(powerupEntity);
                                var powerup = f.Unsafe.GetPointer<Powerup>(powerupEntity);

                                powerupTransform->Position = koopaTransform->Position + FPVector2.Down * FP._0_20;
                                powerup->Initialize(15);

                                enemy->IsActive = false;
                                enemy->IsDead = true;

                            } else {
                                koopa->EnterShell(f, filter.Entity, hit.Entity, false);
                            }
                            mario->DoEntityBounce = true;
                            holdable->PreviousHolder = hit.Entity;
                            koopa->IgnoreOwnerFrames = 5;

                        } else {
                            // Landed on a Spiny, do damage.
                            if (mario->IsCrouchedInShell) {
                                mario->FacingRight = damageDirection.X < 0;
                                marioPhysicsObject->Velocity.X = 0;

                            } else if (mario->IsDamageable) {
                                mario->Powerdown(f, hit.Entity, false);
                                if (!koopa->IsInShell) {
                                    enemy->FacingRight = damageDirection.X > 0;
                                }
                            }
                        }
                    } else {
                        // Damage
                        if (mario->IsCrouchedInShell) {
                            mario->FacingRight = damageDirection.X < 0;
                            marioPhysicsObject->Velocity.X = 0;

                        } else if (mario->IsDamageable) {
                            mario->Powerdown(f, hit.Entity, false);
                            if (!koopa->IsInShell) {
                                enemy->FacingRight = damageDirection.X > 0;
                            }
                        }
                    }
                } else {
                    // Stationary in shell, always kick (if we cant pick it up)
                    if (mario->CanPickupItem(f, hit.Entity)) {
                        filter.Holdable->Pickup(f, filter.Entity, hit.Entity);
                    } else {
                        koopa->Kick(f, filter.Entity, hit.Entity, marioPhysicsObject->Velocity.X / 3);
                        enemy->FacingRight = filter.Transform->Position.X > marioTransform->Position.X;
                    }
                }
            } else if (f.Unsafe.TryGetPointer(hit.Entity, out Enemy* otherEnemy)) {
                if (f.Unsafe.TryGetPointer(hit.Entity, out Koopa* otherKoopa)) {
                    if (!otherEnemy->IsAlive) {
                        return;
                    }

                    bool turn = true;
                    if (koopa->IsKicked || beingHeld) {
                        // Destroy them
                        otherKoopa->Kill(f, hit.Entity, filter.Entity, false);
                        turn = false;
                    }
                    if (otherKoopa->IsKicked || beingHeld || f.Exists(f.Get<Holdable>(hit.Entity).Holder)) {
                        // Destroy ourselves
                        otherKoopa->Kill(f, filter.Entity, hit.Entity, false);
                        turn = false;
                    }

                    if (turn) {
                        var transform = filter.Transform;
                        var otherTransform = f.Unsafe.GetPointer<Transform2D>(hit.Entity);

                        QuantumUtils.UnwrapWorldLocations(f, transform->Position, otherTransform->Position, out var ourPos, out var theirPos);
                        bool right = ourPos.X > theirPos.X;
                        enemy->FacingRight = right;
                        otherEnemy->FacingRight = !right;
                    }
                } else if (f.Unsafe.TryGetPointer(hit.Entity, out Goomba* goomba)) {
                    if (!otherEnemy->IsAlive) {
                        return;
                    }

                    if (koopa->IsKicked || beingHeld) {
                        // Destroy them
                        goomba->Kill(f, hit.Entity, filter.Entity, true);
                        if (beingHeld) {
                            koopa->Kill(f, filter.Entity, hit.Entity, true);
                        }

                    } else {
                        var transform = filter.Transform;
                        var otherTransform = f.Unsafe.GetPointer<Transform2D>(hit.Entity);

                        QuantumUtils.UnwrapWorldLocations(f, transform->Position, otherTransform->Position, out var ourPos, out var theirPos);
                        bool right = ourPos.X > theirPos.X;
                        enemy->FacingRight = right;
                        otherEnemy->FacingRight = !right;
                    }
                }
            }
        }

        public void OnThrowHoldable(Frame f, EntityRef entity, EntityRef marioEntity, QBoolean crouching) {
            if (!f.Unsafe.TryGetPointer(entity, out Koopa* koopa)
                || !f.Unsafe.TryGetPointer(entity, out Enemy* enemy)
                || !f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)
                || !f.Unsafe.TryGetPointer(marioEntity, out MarioPlayer* mario)
                || !f.Unsafe.TryGetPointer(marioEntity, out PhysicsObject* marioPhysics)) {
                return;
            }

            physicsObject->Velocity.Y = 0;
            if (crouching) {
                physicsObject->Velocity.X = mario->FacingRight ? 1 : -1;
                koopa->CurrentSpeed = 0;
            } else {
                koopa->IsKicked = true;
                koopa->CurrentSpeed = koopa->KickSpeed + FPMath.Abs(marioPhysics->Velocity.X / 3);
                f.Events.MarioPlayerThrewObject(f, marioEntity, mario, entity);
            }
            enemy->FacingRight = mario->FacingRight;
            koopa->IgnoreOwnerFrames = 15;
        }

        public void OnEnemyRespawned(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out Koopa* koopa)) {
                koopa->Respawn(f, entity);
            }
        }

        public void OnEntityBumped(Frame f, EntityRef entity, EntityRef blockBump) {
            if (!f.Unsafe.TryGetPointer(entity, out Transform2D* transform)
                || !f.Unsafe.TryGetPointer(entity, out Koopa* koopa)
                || !f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)
                || !f.TryGet(entity, out Enemy enemy)
                || !f.TryGet(blockBump, out Transform2D bumpTransform)
                || !enemy.IsAlive) {

                return;
            }

            koopa->IsInShell = true; // Force sound effect off
            koopa->EnterShell(f, entity, blockBump, true);
            f.Events.PlayComboSound(f, entity, 0);

            QuantumUtils.UnwrapWorldLocations(f, transform->Position, bumpTransform.Position, out FPVector2 ourPos, out FPVector2 theirPos);
            physicsObject->Velocity = new FPVector2(
                ourPos.X > theirPos.X ? 1 : -1,
                FP.FromString("5.5")
            );
            physicsObject->IsTouchingGround = false;
        }
    }
}