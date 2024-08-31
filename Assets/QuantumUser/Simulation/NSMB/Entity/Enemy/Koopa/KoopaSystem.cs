using Photon.Deterministic;
using Quantum.Collections;
using UnityEngine;
using static IInteractableTile;
using static UnityEngine.EventSystems.EventTrigger;

namespace Quantum {

    public unsafe class KoopaSystem : SystemMainThreadFilterStage<KoopaSystem.Filter>, ISignalOnThrowHoldable, ISignalOnEnemyRespawned, ISignalOnEntityBumped, ISignalOnBobombExplodeEntity {
        public struct Filter {
            public EntityRef Entity;
            public Enemy* Enemy;
            public Koopa* Koopa;
            public Transform2D* Transform;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* Collider;
            public Holdable* Holdable;
        }

        public override void OnInit(Frame f) {
            InteractionSystem.RegisterInteraction<Koopa, Goomba>(OnKoopaGoombaInteraction);
            InteractionSystem.RegisterInteraction<Koopa, Koopa>(OnKoopaKoopaInteraction);
            InteractionSystem.RegisterInteraction<Koopa, MarioPlayer>(OnKoopaMarioInteraction);
            InteractionSystem.RegisterInteraction<Koopa, Bobomb>(OnKoopaBobombInteraction);
            InteractionSystem.RegisterInteraction<Koopa, PiranhaPlant>(OnKoopaPiranhaPlantInteraction);
            InteractionSystem.RegisterInteraction<Koopa, Boo>(OnKoopaBooInteraction);
            InteractionSystem.RegisterInteraction<Koopa, Projectile>(OnKoopaProjectileInteraction);
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
                        }
                    }

                    f.Events.PlayBumpSound(f, filter.Entity);
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
                FPVector2 checkPosition = transform->Position + (FPVector2.Up * FP._0_10) + (FPVector2.Right * FP._0_10 * (enemy->FacingRight ? 1 : -1));
                if (!PhysicsObjectSystem.Raycast(f, stage, checkPosition, FPVector2.Down, FP._0_33, out var hit)) {
                    enemy->FacingRight = !enemy->FacingRight;
                }
            }
        }

        public void OnKoopaGoombaInteraction(Frame f, EntityRef koopaEntity, EntityRef goombaEntity) {
            var koopa = f.Unsafe.GetPointer<Koopa>(koopaEntity);
            var goomba = f.Unsafe.GetPointer<Goomba>(goombaEntity);
            bool beingHeld = f.Exists(f.Get<Holdable>(koopaEntity).Holder);

            if (koopa->IsKicked || beingHeld) {
                // Destroy them
                goomba->Kill(f, goombaEntity, koopaEntity, true);
                if (beingHeld) {
                    koopa->Kill(f, koopaEntity, goombaEntity, true);
                }
            } else {
                EnemySystem.EnemyBumpTurnaround(f, koopaEntity, goombaEntity);
            }
        }

        public void OnKoopaKoopaInteraction(Frame f, EntityRef koopaEntityA, EntityRef koopaEntityB) {
            var koopaA = f.Unsafe.GetPointer<Koopa>(koopaEntityA);
            var koopaB = f.Unsafe.GetPointer<Koopa>(koopaEntityB);

            bool eitherBeingHeld = f.Exists(f.Get<Holdable>(koopaEntityA).Holder) || f.Exists(f.Get<Holdable>(koopaEntityB).Holder);

            bool turn = true;
            if (koopaA->IsKicked || eitherBeingHeld) {
                // Destroy them
                koopaB->Kill(f, koopaEntityB, koopaEntityA, false);
                turn = false;
            }
            if (koopaB->IsKicked || eitherBeingHeld) {
                // Destroy ourselves
                koopaA->Kill(f, koopaEntityA, koopaEntityB, false);
                turn = false;
            }

            if (turn) {
                EnemySystem.EnemyBumpTurnaround(f, koopaEntityA, koopaEntityB);
            }
        }

        public void OnKoopaBobombInteraction(Frame f, EntityRef koopaEntity, EntityRef bobombEntity) {
            var koopa = f.Unsafe.GetPointer<Koopa>(koopaEntity);
            var bobomb = f.Unsafe.GetPointer<Bobomb>(bobombEntity);

            bool eitherBeingHeld = f.Exists(f.Get<Holdable>(koopaEntity).Holder) || f.Exists(f.Get<Holdable>(bobombEntity).Holder);

            bool turn = true;
            if (koopa->IsKicked || eitherBeingHeld) {
                // Destroy them
                bobomb->Kill(f, bobombEntity, koopaEntity, true);
                turn = false;
            }
            var bobombPhysicsObject = f.Get<PhysicsObject>(bobombEntity);
            if ((bobomb->CurrentDetonationFrames > 0 && koopa->IsKicked && FPMath.Abs(bobombPhysicsObject.Velocity.X) > 1) || eitherBeingHeld) {
                // Destroy ourselves
                koopa->Kill(f, koopaEntity, bobombEntity, true);
                turn = false;
            }

            if (turn) {
                EnemySystem.EnemyBumpTurnaround(f, koopaEntity, bobombEntity);
            }
        }

        public void OnKoopaMarioInteraction(Frame f, EntityRef koopaEntity, EntityRef marioEntity) {
            var koopa = f.Unsafe.GetPointer<Koopa>(koopaEntity);
            var koopaHoldable = f.Unsafe.GetPointer<Holdable>(koopaEntity);
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            bool beingHeld = f.Exists(koopaHoldable->Holder);

            if (beingHeld || (koopaHoldable->PreviousHolder == marioEntity && koopaHoldable->IgnoreOwnerFrames > 0)) {
                return;
            }

            var koopaEnemy = f.Unsafe.GetPointer<Enemy>(koopaEntity);
            var koopaTransform = f.Get<Transform2D>(koopaEntity);
            var koopaPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(koopaEntity);
            var koopaCollider = f.Get<PhysicsCollider2D>(koopaEntity);
            var marioTransform = f.Get<Transform2D>(marioEntity);
            var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);

            // Mario touched an alive koopa.
            QuantumUtils.UnwrapWorldLocations(f, koopaTransform.Position + ((koopaCollider.Shape.Centroid.Y - koopaCollider.Shape.Box.Extents.Y) * FPVector2.Up), marioTransform.Position, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;
            bool attackedFromAbove = FPVector2.Dot(damageDirection, FPVector2.Up) > FP._0_25;

            if (mario->InstakillsEnemies(*marioPhysicsObject, true)) {
                koopa->Kill(f, koopaEntity, marioEntity, true);
                return;
            }

            bool groundpounded = attackedFromAbove && mario->IsGroundpoundActive && mario->CurrentPowerupState != PowerupState.MiniMushroom;
            bool isSpiny = koopa->IsSpiny && !koopa->IsFlipped;
            if (isSpiny) {
                // Do damage
                if (mario->IsCrouchedInShell) {
                    mario->FacingRight = damageDirection.X < 0;
                    marioPhysicsObject->Velocity.X = 0;

                } else if (mario->IsDamageable) {
                    mario->Powerdown(f, marioEntity, false);
                    if (!koopa->IsInShell) {
                        koopaEnemy->FacingRight = damageDirection.X > 0;
                    }
                }
            } else {
                // Normal collision rules
                if (groundpounded) {
                    if (koopa->SpawnPowerupWhenStomped.IsValid && f.TryFindAsset(koopa->SpawnPowerupWhenStomped, out PowerupAsset powerup)) {
                        // Powerup (for blue koopa): give to mario immediately
                        PowerupReserveResult reserve = PowerupSystem.CollectPowerup(f, marioEntity, mario, marioPhysicsObject, powerup);
                        koopaEnemy->IsActive = false;
                        koopaEnemy->IsDead = true;
                        koopaPhysicsObject->IsFrozen = true;
                        f.Events.MarioPlayerCollectedPowerup(f, koopaEntity, *mario, reserve, powerup);

                    } else {
                        // Kick
                        koopa->IsInShell = true;
                        koopa->IsKicked = false;
                        koopa->EnterShell(f, koopaEntity, marioEntity, false);
                        koopa->Kick(f, koopaEntity, marioEntity, 3);
                        koopaPhysicsObject->Velocity.Y = 2;
                    }
                } else if (koopa->IsKicked || !koopa->IsInShell) {
                    // Moving (either in shell, or walking)
                    if (attackedFromAbove) {
                        // Enter Shell
                        if (koopa->SpawnPowerupWhenStomped.IsValid) {
                            PowerupAsset powerupAsset = f.FindAsset(koopa->SpawnPowerupWhenStomped);
                            EntityRef newPowerup = f.Create(powerupAsset.Prefab);
                            var powerupTransform = f.Unsafe.GetPointer<Transform2D>(newPowerup);
                            var powerup = f.Unsafe.GetPointer<Powerup>(newPowerup);
                            var powerupPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(newPowerup);

                            powerupTransform->Position = koopaTransform.Position + FPVector2.Down * FP._0_20;
                            powerup->Initialize(f, newPowerup, 15);
                            powerupPhysicsObject->DisableCollision = false;

                            koopaEnemy->IsActive = false;
                            koopaEnemy->IsDead = true;
                            koopaPhysicsObject->IsFrozen = true;
                            f.Events.KoopaEnteredShell(f, koopaEntity);
                        } else {
                            koopa->EnterShell(f, koopaEntity, marioEntity, false);
                        }
                        mario->DoEntityBounce = true;
                        koopaHoldable->PreviousHolder = marioEntity;
                        koopaHoldable->IgnoreOwnerFrames = 5;
                    } else {
                        // Damage
                        if (mario->IsCrouchedInShell) {
                            mario->FacingRight = damageDirection.X < 0;
                            marioPhysicsObject->Velocity.X = 0;

                        } else if (mario->IsDamageable) {
                            mario->Powerdown(f, marioEntity, false);
                            if (!koopa->IsInShell) {
                                koopaEnemy->FacingRight = damageDirection.X > 0;
                            }
                        }
                    }
                } else {
                    // Stationary in shell, always kick (if we cant pick it up)
                    if (mario->CanPickupItem(f, marioEntity)) {
                        koopaHoldable->Pickup(f, koopaEntity, marioEntity);
                    } else {
                        koopa->Kick(f, koopaEntity, marioEntity, marioPhysicsObject->Velocity.X / 3);
                        koopaEnemy->FacingRight = koopaTransform.Position.X > marioTransform.Position.X;
                    }
                }
            }
        }

        public void OnKoopaProjectileInteraction(Frame f, EntityRef koopaEntity, EntityRef projectileEntity) {
            var projectileAsset = f.FindAsset(f.Get<Projectile>(projectileEntity).Asset);

            switch (projectileAsset.Effect) {
            case ProjectileEffectType.Knockback: {
                f.Unsafe.GetPointer<Koopa>(koopaEntity)->Kill(f, koopaEntity, projectileEntity, true);
                break;
            }
            case ProjectileEffectType.Freeze: {
                // TODO
                break;
            }
            }

            if (projectileAsset.DestroyOnHit) {
                ProjectileSystem.Destroy(f, projectileEntity, projectileAsset.DestroyParticleEffect);
            }
        }

        public void OnKoopaBooInteraction(Frame f, EntityRef koopaEntity, EntityRef booEntity) {
            var koopa = f.Unsafe.GetPointer<Koopa>(koopaEntity);
            var holdable = f.Unsafe.GetPointer<Holdable>(koopaEntity);

            if (koopa->IsKicked) {
                // Kill boo
                var boo = f.Unsafe.GetPointer<Boo>(booEntity);
                boo->Kill(f, booEntity, koopaEntity, true);
            }
        }

        public void OnKoopaPiranhaPlantInteraction(Frame f, EntityRef koopaEntity, EntityRef piranhaPlantEntity) {
            var koopa = f.Unsafe.GetPointer<Koopa>(koopaEntity);
            var holdable = f.Unsafe.GetPointer<Holdable>(koopaEntity);

            bool beingHeld = f.Exists(holdable->Holder);
            if (koopa->IsKicked || beingHeld) {
                // Kill piranha plant
                var piranhaPlant = f.Unsafe.GetPointer<PiranhaPlant>(piranhaPlantEntity);
                piranhaPlant->Kill(f, piranhaPlantEntity, koopaEntity, true);

                if (beingHeld) {
                    // Kill self, too.
                    koopa->Kill(f, koopaEntity, piranhaPlantEntity, true);
                }
            } else {
                // Turn
                EnemySystem.EnemyBumpTurnaround(f, koopaEntity, piranhaPlantEntity, false);
            }
        }

        public void OnThrowHoldable(Frame f, EntityRef entity, EntityRef marioEntity, QBoolean crouching) {
            if (!f.Unsafe.TryGetPointer(entity, out Koopa* koopa)
                || !f.Unsafe.TryGetPointer(entity, out Holdable* holdable)
                || !f.Unsafe.TryGetPointer(entity, out Enemy* enemy)
                || !f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)
                || !f.Unsafe.TryGetPointer(marioEntity, out MarioPlayer* mario)
                || !f.Unsafe.TryGetPointer(marioEntity, out PhysicsObject* marioPhysics)) {
                return;
            }

            koopa->WakeupFrames = 15 * 60;
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
            holdable->IgnoreOwnerFrames = 15;
        }

        public void OnEnemyRespawned(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out Koopa* koopa)) {
                koopa->Respawn(f, entity);
            }
        }

        public void OnEntityBumped(Frame f, EntityRef entity, FPVector2 position, EntityRef bumpOwner) {
            if (!f.Unsafe.TryGetPointer(entity, out Transform2D* transform)
                || !f.Unsafe.TryGetPointer(entity, out Koopa* koopa)
                || !f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)
                || !f.TryGet(entity, out Enemy enemy)
                || !enemy.IsAlive
                || !f.TryGet(entity, out Holdable holdable)
                || f.Exists(holdable.Holder)
                || holdable.IgnoreOwnerFrames > 0) {

                return;
            }

            koopa->IsInShell = true; // Force sound effect off
            koopa->EnterShell(f, entity, bumpOwner, true);
            f.Events.PlayComboSound(f, entity, 0);

            QuantumUtils.UnwrapWorldLocations(f, transform->Position, position, out FPVector2 ourPos, out FPVector2 theirPos);
            physicsObject->Velocity = new FPVector2(
                ourPos.X > theirPos.X ? 1 : -1,
                FP.FromString("5.5")
            );
            physicsObject->IsTouchingGround = false;
        }

        public void OnBobombExplodeEntity(Frame f, EntityRef bobomb, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out Koopa* koopa)) {
                koopa->Kill(f, entity, bobomb, true);
            }
        }
    }
}