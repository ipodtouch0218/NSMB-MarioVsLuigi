using Photon.Deterministic;
using UnityEngine;

namespace Quantum {

    public unsafe class BobombSystem : SystemMainThreadFilter<BobombSystem.Filter>, ISignalOnEntityBumped, ISignalOnEnemyRespawned, ISignalOnThrowHoldable, 
        ISignalOnBobombExplodeEntity, ISignalOnIceBlockBroken, ISignalOnEnemyKilledByStageReset {
        
        public struct Filter {
            public EntityRef Entity;
            public Bobomb* Bobomb;
            public Enemy* Enemy;
            public Transform2D* Transform;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* Collider;
            public Holdable* Holdable;
            public Freezable* Freezable;
        }

        public override void OnInit(Frame f) {
            f.Context.RegisterInteraction<Bobomb, Bobomb>(EnemySystem.EnemyBumpTurnaround);
            f.Context.RegisterInteraction<Bobomb, Goomba>(EnemySystem.EnemyBumpTurnaround);
            f.Context.RegisterInteraction<Bobomb, PiranhaPlant>(EnemySystem.EnemyBumpTurnaroundOnlyFirst);
            f.Context.RegisterInteraction<Bobomb, MarioPlayer>(OnBobombMarioInteraction);
            f.Context.RegisterInteraction<Bobomb, Projectile>(OnBobombProjectileInteraction);
            f.Context.RegisterInteraction<Bobomb, IceBlock>(OnBobombIceBlockInteraction);
        }

        public override void Update(Frame f, ref Filter filter) {
            var bobomb = filter.Bobomb;
            var enemy = filter.Enemy;

            if (!enemy->IsAlive
                || filter.Freezable->IsFrozen(f)) {
                return;
            }

            bool lit = bobomb->CurrentDetonationFrames > 0;
            if (lit) {
                if (QuantumUtils.Decrement(ref bobomb->CurrentDetonationFrames)) {
                    Explode(f, ref filter);
                    return;
                }
            }

            var holdable = filter.Holdable;
            if (f.Exists(holdable->Holder)) {
                return;
            }

            // Turn around when hitting a wall.
            var physicsObject = filter.PhysicsObject;
            if (physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall) {
                enemy->ChangeFacingRight(f, filter.Entity, physicsObject->IsTouchingLeftWall);
                physicsObject->Velocity.X = (lit ? FPMath.Abs(physicsObject->PreviousFrameVelocity.X) : bobomb->Speed) * (enemy->FacingRight ? 1 : -1);
            }

            // Friction
            if (physicsObject->IsTouchingGround && lit) {
                physicsObject->Velocity.X *= Constants._0_95;
            }

            // Walking
            if (!lit) {
                physicsObject->Velocity.X = bobomb->Speed * (enemy->FacingRight ? 1 : -1);
            }
        }

        private static void Light(Frame f, EntityRef entity, Bobomb* bobomb, bool stomp) {
            if (bobomb->CurrentDetonationFrames > 0) {
                return;
            }

            bobomb->CurrentDetonationFrames = bobomb->DetonationFrames;
            f.Unsafe.GetPointer<PhysicsObject>(entity)->Velocity.X = 0;

            f.Events.BobombLit(f, entity, stomp);
        }

        private static void Explode(Frame f, ref Filter filter) {
            var enemy = filter.Enemy;
            var bobomb = filter.Bobomb;
            var transform = filter.Transform;
            var holdable = filter.Holdable;
            var physicsObject = filter.PhysicsObject;

            // Hit players
            Shape2D shape = Shape2D.CreateCircle(bobomb->ExplosionRadius);
            var hits = f.Physics2D.OverlapShape(*transform, shape);
            for (int i = 0; i < hits.Count; i++) {
                var hit = hits[i];
                if (hit.Entity == filter.Entity) {
                    continue;
                }

                f.Signals.OnBobombExplodeEntity(filter.Entity, hit.Entity);
            }

            // Destroy tiles
            int sizeTiles = FPMath.FloorToInt(bobomb->ExplosionRadius * 2);
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            Vector2Int origin = QuantumUtils.WorldToRelativeTile(stage, transform->Position + filter.Collider->Shape.Centroid);
            for (int x = -sizeTiles; x <= sizeTiles; x++) {
                for (int y = -sizeTiles; y <= sizeTiles; y++) {
                    // Taxicab distance
                    if (FPMath.Abs(x) + FPMath.Abs(y) > sizeTiles) {
                        continue;
                    }

                    Vector2Int tilePos = origin + new Vector2Int(x, y);
                    StageTileInstance tileInstance = stage.GetTileRelative(f, tilePos);
                    StageTile tile = f.FindAsset(tileInstance.Tile);
                    if (tile is IInteractableTile it) {
                        it.Interact(f, filter.Entity, IInteractableTile.InteractionDirection.Up, tilePos, tileInstance, out _);
                    }
                }
            }

            if (f.Exists(holdable->Holder)) {
                var mario = f.Unsafe.GetPointer<MarioPlayer>(holdable->Holder);
                mario->HeldEntity = default;
                holdable->PreviousHolder = default;
                holdable->Holder = default;
            }

            enemy->IsDead = true;
            enemy->IsActive = false;
            physicsObject->Velocity = FPVector2.Zero;
            physicsObject->IsFrozen = true;
            f.Events.BobombExploded(f, filter.Entity);
        }

        #region Interactions
        public static void OnBobombMarioInteraction(Frame f, EntityRef bobombEntity, EntityRef marioEntity) {
            var bobombHoldable = f.Unsafe.GetPointer<Holdable>(bobombEntity);
            var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);

            // Temporary invincibility, we dont want to spam the kick sound
            if (f.Exists(bobombHoldable->Holder) 
                || (bobombHoldable->PreviousHolder == marioEntity && bobombHoldable->IgnoreOwnerFrames > 0)) {
                return;
            }
            
            var bobomb = f.Unsafe.GetPointer<Bobomb>(bobombEntity);
            var bobombTransform = f.Unsafe.GetPointer<Transform2D>(bobombEntity);
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var marioTransform = f.Unsafe.GetPointer<Transform2D>(marioEntity);

            // Special insta-kill cases
            if (mario->InstakillsEnemies(marioPhysicsObject, true)) {
                bobomb->Kill(f, bobombEntity, marioEntity, true);    
                return;
            }

            QuantumUtils.UnwrapWorldLocations(f, bobombTransform->Position + FPVector2.Up * FP._0_10, marioTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            bool fromRight = ourPos.X < theirPos.X;

            FPVector2 damageDirection = (theirPos - ourPos).Normalized;
            bool attackedFromAbove = FPVector2.Dot(damageDirection, FPVector2.Up) > FP._0_33;

            // Normal interactions
            if (bobomb->CurrentDetonationFrames > 0) {
                if (mario->CanPickupItem(f, marioEntity)) {
                    // Pickup by player
                    bobombHoldable->Pickup(f, bobombEntity, marioEntity);
                } else {
                    // Kicked by player
                    bobomb->Kick(f, bobombEntity, marioEntity, marioPhysicsObject->Velocity.X / 3);
                }
            } else {
                if (attackedFromAbove) {
                    // Light
                    bool mini = mario->CurrentPowerupState == PowerupState.MiniMushroom;
                    if (!mini || mario->IsGroundpoundActive) {
                        Light(f, bobombEntity, bobomb, mini || !mario->IsGroundpoundActive);
                    }

                    if (!mini && mario->IsGroundpoundActive) {
                        bobomb->Kick(f, bobombEntity, marioEntity, marioPhysicsObject->Velocity.X / 3);
                    } else {
                        mario->DoEntityBounce = true;
                        mario->IsGroundpounding = false;
                    }
                    mario->IsDrilling = false;

                } else if (mario->IsCrouchedInShell) {
                    // Bounce off blue shell crouched player
                    var bobombEnemy = f.Unsafe.GetPointer<Enemy>(bobombEntity);
                    bobombEnemy->ChangeFacingRight(f, bobombEntity, damageDirection.X < 0);
                    marioPhysicsObject->Velocity.X = 0;
                    return;

                } else if (mario->IsDamageable) {
                    // Damage
                    var bobombEnemy = f.Unsafe.GetPointer<Enemy>(bobombEntity);
                    mario->Powerdown(f, marioEntity, false);
                    bobombEnemy->ChangeFacingRight(f, bobombEntity, damageDirection.X > 0);
                }
            } 
        }

        public static void OnBobombProjectileInteraction(Frame f, EntityRef bobombEntity, EntityRef projectileEntity) {
            var bobomb = f.Unsafe.GetPointer<Bobomb>(bobombEntity);
            var projectileAsset = f.FindAsset(f.Unsafe.GetPointer<Projectile>(projectileEntity)->Asset);

            switch (projectileAsset.Effect) {
            case ProjectileEffectType.Knockback: {
                if (bobomb->CurrentDetonationFrames > 0) {
                    bobomb->Kick(f, bobombEntity, projectileEntity, 0);
                } else {
                    Light(f, bobombEntity, bobomb, false);
                }
                break;
            }
            case ProjectileEffectType.Freeze: {
                IceBlockSystem.Freeze(f, bobombEntity);
                break;
            }
            }

            if (projectileAsset.DestroyOnHit) {
                ProjectileSystem.Destroy(f, projectileEntity, projectileAsset.DestroyParticleEffect);
            }
        }

        public static void OnBobombIceBlockInteraction(Frame f, EntityRef bobombEntity, EntityRef iceBlockEntity, PhysicsContact contact) {
            var bobomb = f.Unsafe.GetPointer<Bobomb>(bobombEntity);
            var iceBlock = f.Unsafe.GetPointer<IceBlock>(iceBlockEntity);

            FP upDot = FPVector2.Dot(contact.Normal, FPVector2.Up);
            if (iceBlock->IsSliding
                && upDot < PhysicsObjectSystem.GroundMaxAngle) {

                bobomb->Kill(f, bobombEntity, iceBlockEntity, true);
            }
        }
        #endregion

        #region Signals
        public void OnEntityBumped(Frame f, EntityRef entity, FPVector2 position, EntityRef bumpOwner) {
            if (!f.Unsafe.TryGetPointer(entity, out Transform2D* transform)
                || !f.Unsafe.TryGetPointer(entity, out Bobomb* bobomb)
                || !f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)
                || !f.Unsafe.TryGetPointer(entity, out Enemy* enemy)
                || !enemy->IsAlive
                || !f.Unsafe.TryGetPointer(entity, out Holdable* holdable)
                || f.Exists(holdable->Holder)) {

                return;
            }

            Light(f, entity, bobomb, true);
            QuantumUtils.UnwrapWorldLocations(f, transform->Position, position, out FPVector2 ourPos, out FPVector2 theirPos);
            physicsObject->Velocity = new FPVector2(
                ourPos.X > theirPos.X ? 1 : -1,
                Constants._5_50
            );
            physicsObject->IsTouchingGround = false;

            f.Events.EntityBlockBumped(f, entity);
        }

        public void OnEnemyRespawned(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out Bobomb* bobomb)) {
                bobomb->Respawn(f, entity);
            }
        }

        public void OnThrowHoldable(Frame f, EntityRef entity, EntityRef marioEntity, QBoolean crouching, QBoolean dropped) {
            if (!f.Unsafe.TryGetPointer(entity, out Bobomb* bobomb)
                || !f.Unsafe.TryGetPointer(entity, out Holdable* holdable)
                || !f.Unsafe.TryGetPointer(entity, out Enemy* enemy)
                || !f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)
                || !f.Unsafe.TryGetPointer(entity, out PhysicsCollider2D* collider)
                || !f.Unsafe.TryGetPointer(entity, out Transform2D* transform)
                || !f.Unsafe.TryGetPointer(marioEntity, out MarioPlayer* mario)
                || !f.Unsafe.TryGetPointer(marioEntity, out PhysicsObject* marioPhysics)) {
                return;
            }

            if (PhysicsObjectSystem.BoxInGround((FrameThreadSafe) f, transform->Position, collider->Shape, entity: entity)) {
                bobomb->Kill(f, entity, marioEntity, true);
                return;
            }

            physicsObject->Velocity.Y = 0;
            if (dropped) {
                physicsObject->Velocity.X = 0;
            } else if (crouching) {
                physicsObject->Velocity.X = mario->FacingRight ? 1 : -1;
            } else {
                physicsObject->Velocity.X = (Constants._4_50 + FPMath.Abs(marioPhysics->Velocity.X / 3)) * (mario->FacingRight ? 1 : -1);
                f.Events.MarioPlayerThrewObject(f, marioEntity, entity);
            }
            enemy->FacingRight = mario->FacingRight;
            holdable->IgnoreOwnerFrames = 15;
        }

        public void OnBobombExplodeEntity(Frame f, EntityRef bobombEntity, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out Bobomb* bobomb)) {
                bobomb->Kill(f, entity, bobombEntity, true);
            }
        }

        public void OnIceBlockBroken(Frame f, EntityRef brokenIceBlock, IceBlockBreakReason breakReason) {
            var iceBlock = f.Unsafe.GetPointer<IceBlock>(brokenIceBlock);
            if (f.Unsafe.TryGetPointer(iceBlock->Entity, out Bobomb* bobomb)) {
                bobomb->Kill(f, iceBlock->Entity, brokenIceBlock, true);
            }
        }

        public void OnEnemyKilledByStageReset(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out Bobomb* bobomb)) {
                if (f.Unsafe.TryGetPointer(entity, out Holdable* holdable)
                    && f.Exists(holdable->Holder)) {
                    // Don't die if being held
                    return;
                }
                bobomb->Kill(f, entity, EntityRef.None, true);
            }
        }
        #endregion
    }
}