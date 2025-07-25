using Photon.Deterministic;
using Quantum.Collections;
using static IInteractableTile;

namespace Quantum {
    [UnityEngine.Scripting.Preserve]
    public unsafe class KoopaSystem : SystemMainThreadEntityFilter<Koopa, KoopaSystem.Filter>, ISignalOnThrowHoldable, ISignalOnEnemyRespawned, ISignalOnEntityBumped,
        ISignalOnBobombExplodeEntity, ISignalOnIceBlockBroken, ISignalOnEnemyKilledByStageReset, ISignalOnEnemyTurnaround, ISignalOnEntityCrushed,
        ISignalOnMarioPlayerBecameInvincible {
       
        public struct Filter {
            public EntityRef Entity;
            public Enemy* Enemy;
            public Koopa* Koopa;
            public Transform2D* Transform;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* Collider;
            public Holdable* Holdable;
            public Freezable* Freezable;
        }

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<Koopa, Goomba>(f, OnKoopaGoombaInteraction);
            f.Context.Interactions.Register<Koopa, Koopa>(f, OnKoopaKoopaInteraction);
            f.Context.Interactions.Register<Koopa, MarioPlayer>(f, OnKoopaMarioInteraction);
            f.Context.Interactions.Register<Koopa, Bobomb>(f, OnKoopaBobombInteraction);
            f.Context.Interactions.Register<Koopa, BulletBill>(f, OnKoopaBulletBillInteraction);
            f.Context.Interactions.Register<Koopa, PiranhaPlant>(f, OnKoopaPiranhaPlantInteraction);
            f.Context.Interactions.Register<Koopa, Boo>(f, OnKoopaBooInteraction);
            f.Context.Interactions.Register<Koopa, Projectile>(f, OnKoopaProjectileInteraction);
            f.Context.Interactions.Register<Koopa, Coin>(f, OnKoopaCoinInteraction);
            f.Context.Interactions.Register<Koopa, IceBlock>(f, OnKoopaIceBlockInteraction);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var entity = filter.Entity;
            var enemy = filter.Enemy;
            var koopa = filter.Koopa;
            var transform = filter.Transform;
            var physicsObject = filter.PhysicsObject;
            var freezable = filter.Freezable;

            // Inactive check
            if (!enemy->IsAlive
                || freezable->IsFrozen(f)) {
                return;
            }

            freezable->IceBlockSize = (koopa->IsSpiny || koopa->IsInShell) ? koopa->IceBlockInShellSize : koopa->IceBlockOutShellSize;

            if (koopa->IsInShell && !koopa->IsKicked) {
                if (QuantumUtils.Decrement(ref koopa->WakeupFrames)) {
                    koopa->IsInShell = false;
                    koopa->IsKicked = false;
                    koopa->IsFlipped = false;
                    koopa->CurrentSpeed = koopa->Speed;
                    enemy->FacingRight = false;
                    koopa->TurnaroundWaitFrames = 18;

                    var holdable = filter.Holdable;
                    if (f.Exists(holdable->Holder)) {
                        var mario = f.Unsafe.GetPointer<MarioPlayer>(holdable->Holder);
                        mario->HeldEntity = default;
                        holdable->PreviousHolder = default;
                        holdable->Holder = default;

                        PhysicsObjectSystem.TryEject(f, entity, stage);
                    }
                }
            }

            // Turn around when hitting a wall.
            if (physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall) {
                enemy->ChangeFacingRight(f, entity, physicsObject->IsTouchingLeftWall);

                if (koopa->IsKicked) {
                    QList<PhysicsContact> contacts = f.ResolveList(physicsObject->Contacts);
                    foreach (var contact in contacts) {
                        FP dot = FPVector2.Dot(contact.Normal, FPVector2.Right);
                        bool right = dot < 0;
                        if (FPMath.Abs(dot) < FP._0_75) {
                            continue;
                        }

                        // Floor tiles.
                        var tileInstance = stage.GetTileRelative(f, contact.Tile);
                        StageTile tile = f.FindAsset(tileInstance.Tile);
                        if (tile is IInteractableTile it) {
                            it.Interact(f, entity, right ? InteractionDirection.Right : InteractionDirection.Left,
                                contact.Tile, tileInstance, out bool tempPlayBumpSound);
                        }
                    }

                    f.Events.PlayBumpSound(entity);
                }
            }

            // Move
            if (!QuantumUtils.Decrement(ref koopa->TurnaroundWaitFrames)) {
                physicsObject->Velocity.X = 0;

            } else if (koopa->IsKicked
                       || !koopa->IsInShell
                       || physicsObject->IsTouchingLeftWall
                       || physicsObject->IsTouchingRightWall
                       || physicsObject->IsTouchingGround) {

                physicsObject->Velocity.X = koopa->CurrentSpeed * (enemy->FacingRight ? 1 : -1);
            }

            if (koopa->DontWalkOfLedges && !koopa->IsInShell && physicsObject->IsTouchingGround) {
                FPVector2 checkPosition = transform->Position + filter.Collider->Shape.Centroid + (FPVector2.Right * FP._0_05 * (enemy->FacingRight ? 1 : -1));
                if (!PhysicsObjectSystem.Raycast(f, stage, checkPosition, FPVector2.Down, FP._0_33, out var hit)) {
                    // Failed to hit a raycast, but check to make sure we don't have a contact point instead.

                    bool turnaround = true;
                    QList<PhysicsContact> contacts = f.ResolveList(physicsObject->Contacts);
                    foreach (var contact in contacts) {
                        if (FPVector2.Dot(contact.Normal, FPVector2.Up) < Constants.PhysicsGroundMaxAngleCos) {
                            // Not on the ground
                            continue;
                        }

                        // Is a ground contact
                        QuantumUtils.UnwrapWorldLocations(stage, transform->Position, contact.Position, out FPVector2 ourPos, out FPVector2 contactPos);
                        if ((enemy->FacingRight && ourPos.X < contactPos.X)
                            || (!enemy->FacingRight && ourPos.X > contactPos.X)) {
                            turnaround = false;
                            break;
                        }
                    }

                    if (turnaround) {
                        enemy->ChangeFacingRight(f, entity, !enemy->FacingRight);
                    }
                }
            }
        }

        #region Interactions
        public static void OnKoopaGoombaInteraction(Frame f, EntityRef koopaEntity, EntityRef goombaEntity) {
            var koopa = f.Unsafe.GetPointer<Koopa>(koopaEntity);
            var goomba = f.Unsafe.GetPointer<Goomba>(goombaEntity);
            bool beingHeld = f.Exists(f.Unsafe.GetPointer<Holdable>(koopaEntity)->Holder);

            if (koopa->IsKicked || beingHeld) {
                // Destroy them
                goomba->Kill(f, goombaEntity, koopaEntity, KillReason.Special);
                if (beingHeld) {
                    koopa->Kill(f, koopaEntity, goombaEntity, KillReason.Special);
                }
            } else {
                EnemySystem.EnemyBumpTurnaround(f, koopaEntity, goombaEntity);
            }
        }

        public static void OnKoopaKoopaInteraction(Frame f, EntityRef koopaEntityA, EntityRef koopaEntityB) {
            var koopaA = f.Unsafe.GetPointer<Koopa>(koopaEntityA);
            var koopaB = f.Unsafe.GetPointer<Koopa>(koopaEntityB);

            bool koopaABeingHeld = f.Exists(f.Unsafe.GetPointer<Holdable>(koopaEntityA)->Holder);
            bool koopaBBeingHeld = f.Exists(f.Unsafe.GetPointer<Holdable>(koopaEntityB)->Holder);
            bool anyDamaged = false;
            if (koopaABeingHeld || koopaBBeingHeld || koopaA->IsKicked) {
                // Destroy B
                koopaB->Kill(f, koopaEntityB, koopaEntityA, KillReason.Special);
                anyDamaged = true;
            }
            if (koopaABeingHeld || koopaBBeingHeld || koopaB->IsKicked) {
                // Destroy A
                koopaA->Kill(f, koopaEntityA, koopaEntityB, KillReason.Special);
                anyDamaged = true;
            }
            
            if (!anyDamaged) {
                EnemySystem.EnemyBumpTurnaround(f, koopaEntityA, koopaEntityB);
            }
        }

        public static void OnKoopaBobombInteraction(Frame f, EntityRef koopaEntity, EntityRef bobombEntity) {
            var koopa = f.Unsafe.GetPointer<Koopa>(koopaEntity);
            var bobomb = f.Unsafe.GetPointer<Bobomb>(bobombEntity);

            bool koopaBeingHeld = f.Exists(f.Unsafe.GetPointer<Holdable>(koopaEntity)->Holder);
            bool bobombBeingHeld = f.Exists(f.Unsafe.GetPointer<Holdable>(bobombEntity)->Holder);

            bool anyDamaged = false;
            if (koopaBeingHeld || bobombBeingHeld || koopa->IsKicked) {
                // Destroy them
                bobomb->Kill(f, bobombEntity, koopaEntity, KillReason.Special);
                anyDamaged = true;
            }
            if (koopaBeingHeld || bobombBeingHeld || (bobomb->CurrentDetonationFrames > 0 && f.Unsafe.GetPointer<PhysicsObject>(bobombEntity)->Velocity.Magnitude > FP._0_05)) {
                // Destroy them
                koopa->Kill(f, koopaEntity, bobombEntity, KillReason.Special);
                anyDamaged = true;
            }

            if (!anyDamaged) {
                EnemySystem.EnemyBumpTurnaround(f, koopaEntity, bobombEntity);
            }
        }

        public static void OnKoopaMarioInteraction(Frame f, EntityRef koopaEntity, EntityRef marioEntity) {
            var koopa = f.Unsafe.GetPointer<Koopa>(koopaEntity);
            var koopaHoldable = f.Unsafe.GetPointer<Holdable>(koopaEntity);
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            bool beingHeld = f.Exists(koopaHoldable->Holder);

            if (beingHeld || (koopaHoldable->PreviousHolder == marioEntity && koopaHoldable->IgnoreOwnerFrames > 0)) {
                return;
            }

            var koopaEnemy = f.Unsafe.GetPointer<Enemy>(koopaEntity);
            var koopaTransform = f.Unsafe.GetPointer<Transform2D>(koopaEntity);
            var koopaPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(koopaEntity);
            var koopaCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(koopaEntity);
            var marioTransform = f.Unsafe.GetPointer<Transform2D>(marioEntity);
            var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);

            // Mario touched an alive koopa.
            QuantumUtils.UnwrapWorldLocations(f, koopaTransform->Position + ((koopaCollider->Shape.Centroid.Y - koopaCollider->Shape.Box.Extents.Y) * FPVector2.Up), marioTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            FPVector2 damageDirection = (theirPos - ourPos).Normalized;
            bool attackedFromAbove = FPVector2.Dot(damageDirection, FPVector2.Up) > FP._0_25;

            bool isSpiny = koopa->IsSpiny && !koopa->IsFlipped;

            if (mario->InstakillsEnemies(marioPhysicsObject, false) || (!isSpiny && mario->InstakillsEnemies(marioPhysicsObject, true))) {
                koopa->Kill(f, koopaEntity, marioEntity, KillReason.Special);
                return;
            }

            bool groundpounded = attackedFromAbove && mario->IsGroundpoundActive && mario->CurrentPowerupState != PowerupState.MiniMushroom;
            if (isSpiny) {
                // Do damage
                if (mario->IsCrouchedInShell) {
                    mario->FacingRight = damageDirection.X < 0;
                    marioPhysicsObject->Velocity.X = 0;

                } else if (mario->IsDamageable) {
                    mario->Powerdown(f, marioEntity, false, koopaEntity);
                    if (!koopa->IsInShell) {
                        koopaEnemy->ChangeFacingRight(f, koopaEntity, damageDirection.X > 0);
                    }
                }
                return;
            }
            
            if (groundpounded) {
                if (koopa->SpawnPowerupWhenStomped.IsValid
                    && f.TryFindAsset(koopa->SpawnPowerupWhenStomped, out PowerupAsset powerup)) {
                    // Powerup (for blue koopa): give to mario immediately
                    PowerupReserveResult result = PowerupSystem.CollectPowerup(f, marioEntity, mario, marioPhysicsObject, powerup);
                    koopaEnemy->IsActive = false;
                    koopaEnemy->IsDead = true;
                    koopaPhysicsObject->IsFrozen = true;
                    f.Events.MarioPlayerCollectedPowerup(marioEntity, result, powerup);

                } else {
                    // Kick
                    koopa->IsInShell = true;
                    koopa->IsKicked = false;
                    koopaEnemy->ChangeFacingRight(f, koopaEntity, ourPos.X > theirPos.X);
                    koopa->EnterShell(f, koopaEntity, marioEntity, false, true);
                    koopa->Kick(f, koopaEntity, marioEntity, 3);
                    koopaPhysicsObject->Velocity.Y = 2;
                }
                return;
            }
            
            if (koopa->IsKicked || !koopa->IsInShell) {
                // Moving (either in shell, or walking)
                if (attackedFromAbove) {
                    // Enter Shell
                    if (!koopa->IsKicked && koopa->SpawnPowerupWhenStomped.IsValid) {
                        PowerupAsset powerupAsset = f.FindAsset(koopa->SpawnPowerupWhenStomped);
                        EntityRef newPowerup = f.Create(powerupAsset.Prefab);
                        var powerupTransform = f.Unsafe.GetPointer<Transform2D>(newPowerup);
                        var coinItem = f.Unsafe.GetPointer<CoinItem>(newPowerup);
                        var powerupPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(newPowerup);

                        powerupTransform->Position = koopaTransform->Position;
                        coinItem->Initialize(f, newPowerup, 15, PowerupSpawnReason.BlueKoopa);
                        powerupPhysicsObject->DisableCollision = false;

                        koopaEnemy->IsActive = false;
                        koopaEnemy->IsDead = true;
                        koopaPhysicsObject->IsFrozen = true;

                    } else if (mario->CurrentPowerupState != PowerupState.MiniMushroom || mario->IsGroundpoundActive) {
                        koopa->EnterShell(f, koopaEntity, marioEntity, false, false);
                    }
                    mario->DoEntityBounce = true;
                    koopaHoldable->PreviousHolder = marioEntity;
                    koopaHoldable->IgnoreOwnerFrames = 5;
                } else {
                    // Damage
                    if (mario->IsCrouchedInShell) {
                        //mario->FacingRight = damageDirection.X < 0;
                        //marioPhysicsObject->Velocity.X = 0;
                        koopa->Kill(f, koopaEntity, marioEntity, KillReason.Special);

                    } else if (mario->IsDamageable) {
                        mario->Powerdown(f, marioEntity, false, koopaEntity);
                        if (!koopa->IsInShell) {
                            koopaEnemy->ChangeFacingRight(f, koopaEntity, damageDirection.X > 0);
                        }
                    }
                }
                return;
            }
            
            // Stationary in shell, always kick (if we cant pick it up)
            if (mario->CanPickupItem(f, marioEntity, koopaEntity)) {
                koopaHoldable->Pickup(f, koopaEntity, marioEntity);
            } else {
                koopa->Kick(f, koopaEntity, marioEntity, FPMath.Abs(marioPhysicsObject->Velocity.X) * FP._0_33);
                koopaEnemy->ChangeFacingRight(f, koopaEntity, ourPos.X > theirPos.X);
            }
        }
        
        public static bool OnKoopaIceBlockInteraction(Frame f, EntityRef koopaEntity, EntityRef iceBlockEntity, PhysicsContact contact) {
            var koopa = f.Unsafe.GetPointer<Koopa>(koopaEntity);
            var iceBlock = f.Unsafe.GetPointer<IceBlock>(iceBlockEntity);

            FP upDot = FPVector2.Dot(contact.Normal, FPVector2.Up);
            if (iceBlock->IsSliding && upDot < Constants.PhysicsGroundMaxAngleCos) {
                koopa->Kill(f, koopaEntity, iceBlockEntity, KillReason.Special);
            }

            if (koopa->IsInShell && koopa->IsKicked) {
                IceBlockSystem.Destroy(f, iceBlockEntity, IceBlockBreakReason.Other);
            }
            return false;
        }

        public static void OnKoopaCoinInteraction(Frame f, EntityRef koopaEntity, EntityRef coinEntity) {
            var koopa = f.Unsafe.GetPointer<Koopa>(koopaEntity);
            var holdable = f.Unsafe.GetPointer<Holdable>(koopaEntity);

            if (!koopa->IsKicked
                || !f.Exists(holdable->PreviousHolder)) {
                return;
            }

            CoinSystem.TryCollectCoin(f, coinEntity, holdable->PreviousHolder);
        }

        public static void OnKoopaProjectileInteraction(Frame f, EntityRef koopaEntity, EntityRef projectileEntity) {
            var projectileAsset = f.FindAsset(f.Unsafe.GetPointer<Projectile>(projectileEntity)->Asset);

            switch (projectileAsset.Effect) {
            case ProjectileEffectType.KillEnemiesAndSoftKnockbackPlayers:
            case ProjectileEffectType.Fire: {
                f.Unsafe.GetPointer<Koopa>(koopaEntity)->Kill(f, koopaEntity, projectileEntity, KillReason.Special);
                break;
            }
            case ProjectileEffectType.Freeze: {
                IceBlockSystem.Freeze(f, koopaEntity);
                break;
            }
            }

            f.Signals.OnProjectileHitEntity(f, projectileEntity, koopaEntity);
        }

        public static void OnKoopaBooInteraction(Frame f, EntityRef koopaEntity, EntityRef booEntity) {
            var koopa = f.Unsafe.GetPointer<Koopa>(koopaEntity);
            var holdable = f.Unsafe.GetPointer<Holdable>(koopaEntity);

            bool beingHeld = f.Exists(holdable->Holder);
            if (beingHeld || koopa->IsKicked) {
                // Kill boo
                var boo = f.Unsafe.GetPointer<Boo>(booEntity);
                boo->Kill(f, booEntity, koopaEntity, KillReason.Special);
            }
            if (beingHeld) {
                // Also kill ourselves
                koopa->Kill(f, koopaEntity, booEntity, KillReason.Special);
            }
        }

        public static void OnKoopaPiranhaPlantInteraction(Frame f, EntityRef koopaEntity, EntityRef piranhaPlantEntity) {
            var koopa = f.Unsafe.GetPointer<Koopa>(koopaEntity);
            var holdable = f.Unsafe.GetPointer<Holdable>(koopaEntity);

            bool beingHeld = f.Exists(holdable->Holder);
            bool anyDamaged = false;
            if (koopa->IsKicked || beingHeld) {
                // Kill piranha plant
                var piranhaPlant = f.Unsafe.GetPointer<PiranhaPlant>(piranhaPlantEntity);
                piranhaPlant->Kill(f, piranhaPlantEntity, koopaEntity, KillReason.Special);
                anyDamaged = true;
            }
            if (beingHeld) { 
                // Kill self, too.
                koopa->Kill(f, koopaEntity, piranhaPlantEntity, KillReason.Special);
                anyDamaged = true;
            }
            
            if (!anyDamaged) {
                // Turn
                EnemySystem.EnemyBumpTurnaround(f, koopaEntity, piranhaPlantEntity, false);
            }
        }

        public static void OnKoopaBulletBillInteraction(Frame f, EntityRef koopaEntity, EntityRef bulletBillEntity) {
            var koopa = f.Unsafe.GetPointer<Koopa>(koopaEntity);
            var holdable = f.Unsafe.GetPointer<Holdable>(koopaEntity);

            bool beingHeld = f.Exists(holdable->Holder);
            if (koopa->IsKicked || beingHeld) {
                // Kill bullet bill
                var bulletBill = f.Unsafe.GetPointer<BulletBill>(bulletBillEntity);
                bulletBill->Kill(f, bulletBillEntity, koopaEntity, KillReason.Normal); // yes, this is the correct kill reason.
            }
            if (beingHeld) {
                koopa->Kill(f, koopaEntity, bulletBillEntity, KillReason.Special);
            }
        }
        #endregion

        #region Signals
        public void OnThrowHoldable(Frame f, EntityRef entity, EntityRef marioEntity, QBoolean crouching, QBoolean dropped) {
            if (!f.Unsafe.TryGetPointer(entity, out Koopa* koopa)
                || !f.Unsafe.TryGetPointer(entity, out Holdable* holdable)
                || !f.Unsafe.TryGetPointer(entity, out Enemy* enemy)
                || !f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)
                || !f.Unsafe.TryGetPointer(entity, out PhysicsCollider2D* collider)
                || !f.Unsafe.TryGetPointer(entity, out Transform2D* transform)
                || !f.Unsafe.TryGetPointer(marioEntity, out MarioPlayer* mario)
                || !f.Unsafe.TryGetPointer(marioEntity, out PhysicsObject* marioPhysics)) {
                return;
            }

            if (PhysicsObjectSystem.BoxInGround(f, transform->Position, collider->Shape, entity: entity)) {
                koopa->Kill(f, entity, marioEntity, KillReason.InWall);
                return;
            }

            physicsObject->Velocity.Y = 0;
            if (dropped) {
                physicsObject->Velocity.X = 0;
                koopa->CurrentSpeed = 0;
            } else if (crouching) {
                physicsObject->Velocity.X = mario->FacingRight ? 1 : -1;
                koopa->CurrentSpeed = 0;
            } else {
                koopa->WakeupFrames = 15 * 60;
                koopa->IsKicked = true;
                koopa->CurrentSpeed = koopa->KickSpeed + FPMath.Abs(marioPhysics->Velocity.X / 3);
                f.Events.MarioPlayerThrewObject(marioEntity, entity);
            }
            enemy->ChangeFacingRight(f, entity, mario->FacingRight);
            holdable->IgnoreOwnerFrames = 15;
        }

        public void OnEnemyRespawned(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out Koopa* koopa)) {
                koopa->Respawn(f, entity);
            }
        }

        public void OnEntityBumped(Frame f, EntityRef entity, FPVector2 position, EntityRef bumpOwner, QBoolean fromBelow) {
            if (!f.Unsafe.TryGetPointer(entity, out Transform2D* transform)
                || !f.Unsafe.TryGetPointer(entity, out Koopa* koopa)
                || !f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)
                || !f.Unsafe.TryGetPointer(entity, out Enemy* enemy)
                || !enemy->IsAlive
                || !f.Unsafe.TryGetPointer(entity, out Holdable* holdable)
                || f.Exists(holdable->Holder)
                || holdable->IgnoreOwnerFrames > 0) {

                return;
            }

            koopa->IsInShell = true; // Force sound effect off
            koopa->EnterShell(f, entity, bumpOwner, true, false);
            f.Events.PlayComboSound(entity, 0);

            QuantumUtils.UnwrapWorldLocations(f, transform->Position, position, out FPVector2 ourPos, out FPVector2 theirPos);
            physicsObject->Velocity = new FPVector2(
                ourPos.X > theirPos.X ? 1 : -1,
                Constants._5_50
            );
            physicsObject->IsTouchingGround = false;
        }

        public void OnBobombExplodeEntity(Frame f, EntityRef bobomb, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out Koopa* koopa)) {
                koopa->Kill(f, entity, bobomb, KillReason.Special);
            }
        }

        public void OnIceBlockBroken(Frame f, EntityRef brokenIceBlock, IceBlockBreakReason breakReason) {
            var iceBlock = f.Unsafe.GetPointer<IceBlock>(brokenIceBlock);
            if (f.Unsafe.TryGetPointer(iceBlock->Entity, out Koopa* koopa)) {
                koopa->Kill(f, iceBlock->Entity, brokenIceBlock, KillReason.Special);
            }
        }

        public void OnEnemyKilledByStageReset(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out Koopa* koopa)) {
                if (f.Unsafe.TryGetPointer(entity, out Holdable* holdable)
                    && f.Exists(holdable->Holder)) {
                    // Don't die if being held
                    return;
                }
                koopa->Kill(f, entity, EntityRef.None, KillReason.InWall);
            }
        }

        public void OnEnemyTurnaround(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out Koopa* koopa) && !koopa->IsInShell && !koopa->IsKicked) {
                koopa->TurnaroundWaitFrames = 9;
            }
        }

        public void OnEntityCrushed(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out Koopa* koopa)) {
                koopa->Kill(f, entity, EntityRef.None, KillReason.InWall);
            }
        }

        public void OnMarioPlayerBecameInvincible(Frame f, EntityRef entity) {
            var mario = f.Unsafe.GetPointer<MarioPlayer>(entity);
            if (f.Unsafe.TryGetPointer(mario->HeldEntity, out Koopa* koopa)) {
                koopa->Kill(f, mario->HeldEntity, entity, KillReason.Special);
            }
        }
        #endregion
    }
}