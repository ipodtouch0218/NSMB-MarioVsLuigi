using Photon.Deterministic;
using UnityEngine;

namespace Quantum {

    public unsafe class BobombSystem : SystemMainThreadFilter<BobombSystem.Filter>, ISignalOnEntityBumped, ISignalOnEnemyRespawned, ISignalOnThrowHoldable, ISignalOnBobombExplodeEntity {
        public struct Filter {
            public EntityRef Entity;
            public Bobomb* Bobomb;
            public Enemy* Enemy;
            public Transform2D* Transform;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* Collider;
            public Holdable* Holdable;
        }

        public override void OnInit(Frame f) {
            EnemySystem.RegisterInteraction<Bobomb, Bobomb>(OnBobombBobombInteraction);
            EnemySystem.RegisterInteraction<Bobomb, MarioPlayer>(OnBobombMarioInteraction);
        }

        public override void Update(Frame f, ref Filter filter) {
            var bobomb = filter.Bobomb;
            var enemy = filter.Enemy;

            if (!enemy->IsAlive) {
                return;
            }

            bool lit = bobomb->CurrentDetonationFrames > 0;

            if (lit) {
                if (QuantumUtils.Decrement(ref bobomb->CurrentDetonationFrames)) {
                    Explode(f, filter);
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
                enemy->FacingRight = physicsObject->IsTouchingLeftWall;

                physicsObject->Velocity.X = (lit ? FPMath.Abs(physicsObject->PreviousVelocity.X) : bobomb->Speed) * (enemy->FacingRight ? 1 : -1);
            }

            // Friction
            if (physicsObject->IsTouchingGround && lit) {
                physicsObject->Velocity *= FP.FromString("0.95");
            }

            // Walking
            if (!lit) {
                physicsObject->Velocity.X = bobomb->Speed * (enemy->FacingRight ? 1 : -1);
            }
        }

        public void OnBobombBobombInteraction(Frame f, EntityRef bobombEntityA, EntityRef bobombEntityB) {
            EnemySystem.EnemyBumpTurnaround(f, bobombEntityA, bobombEntityB);
        }

        public void OnBobombMarioInteraction(Frame f, EntityRef bobombEntity, EntityRef marioEntity) {
            var bobombHoldable = f.Unsafe.GetPointer<Holdable>(bobombEntity);
            var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);

            // Temporary invincibility, we dont want to spam the kick sound
            if (f.Exists(bobombHoldable->Holder) 
                || (bobombHoldable->PreviousHolder == marioEntity && bobombHoldable->IgnoreOwnerFrames > 0)) {
                return;
            }
            
            var bobomb = f.Unsafe.GetPointer<Bobomb>(bobombEntity);
            var bobombTransform = f.Get<Transform2D>(bobombEntity);
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            var marioTransform = f.Get<Transform2D>(marioEntity);

            // Special insta-kill cases
            if (mario->InstakillsEnemies(*marioPhysicsObject)) {
                bobomb->Kill(f, bobombEntity, marioEntity, true);    
                return;
            }

            QuantumUtils.UnwrapWorldLocations(f, bobombTransform.Position + FPVector2.Up * FP._0_10, marioTransform.Position, out FPVector2 ourPos, out FPVector2 theirPos);
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
                        Light(f, bobombEntity, bobomb);
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
                    bobombEnemy->FacingRight = damageDirection.X < 0;
                    marioPhysicsObject->Velocity.X = 0;
                    return;

                } else if (mario->IsDamageable) {
                    // Damage
                    var bobombEnemy = f.Unsafe.GetPointer<Enemy>(bobombEntity);
                    mario->Powerdown(f, marioEntity, false);
                    bobombEnemy->FacingRight = damageDirection.X > 0;
                }
            } 
        }

        private static void Light(Frame f, EntityRef entity, Bobomb* bobomb) {
            if (bobomb->CurrentDetonationFrames > 0) {
                return;
            }

            bobomb->CurrentDetonationFrames = bobomb->DetonationFrames;
            f.Unsafe.GetPointer<PhysicsObject>(entity)->Velocity.X = 0;

            f.Events.BobombLit(f, entity);
        }

        private static void Explode(Frame f, Filter filter) {
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

        public void OnEntityBumped(Frame f, EntityRef entity, EntityRef blockBump) {
            if (!f.Unsafe.TryGetPointer(entity, out Transform2D* transform)
                || !f.Unsafe.TryGetPointer(entity, out Bobomb* bobomb)
                || !f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)
                || !f.TryGet(entity, out Enemy enemy)
                || !f.TryGet(blockBump, out Transform2D bumpTransform)
                || !enemy.IsAlive
                || !f.TryGet(entity, out Holdable holdable)
                || f.Exists(holdable.Holder)) {

                return;
            }

            Light(f, entity, bobomb);
            QuantumUtils.UnwrapWorldLocations(f, transform->Position, bumpTransform.Position, out FPVector2 ourPos, out FPVector2 theirPos);
            physicsObject->Velocity = new FPVector2(
                ourPos.X > theirPos.X ? 1 : -1,
                FP.FromString("5.5")
            );
            physicsObject->IsTouchingGround = false;
        }

        public void OnEnemyRespawned(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out Bobomb* bobomb)) {
                bobomb->Respawn(f, entity);
            }
        }

        public void OnThrowHoldable(Frame f, EntityRef entity, EntityRef marioEntity, QBoolean crouching) {
            if (!f.Unsafe.TryGetPointer(entity, out Bobomb* bobomb)
                || !f.Unsafe.TryGetPointer(entity, out Holdable* holdable)
                || !f.Unsafe.TryGetPointer(entity, out Enemy* enemy)
                || !f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)
                || !f.Unsafe.TryGetPointer(marioEntity, out MarioPlayer* mario)
                || !f.Unsafe.TryGetPointer(marioEntity, out PhysicsObject* marioPhysics)) {
                return;
            }

            physicsObject->Velocity.Y = 0;
            if (crouching) {
                physicsObject->Velocity.X = mario->FacingRight ? 1 : -1;
            } else {
                physicsObject->Velocity.X = (FP.FromString("4.5") + FPMath.Abs(marioPhysics->Velocity.X / 3)) * (mario->FacingRight ? 1 : -1);
                f.Events.MarioPlayerThrewObject(f, marioEntity, mario, entity);
            }
            enemy->FacingRight = mario->FacingRight;
            holdable->IgnoreOwnerFrames = 15;
        }

        public void OnBobombExplodeEntity(Frame f, EntityRef bobombEntity, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out Bobomb* bobomb)) {
                bobomb->Kill(f, entity, bobombEntity, true);
            }
        }
    }
}