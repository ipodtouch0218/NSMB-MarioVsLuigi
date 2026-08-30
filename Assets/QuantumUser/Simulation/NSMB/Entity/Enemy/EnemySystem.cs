using Photon.Deterministic;

namespace Quantum {
    public unsafe class EnemySystem : SystemMainThreadEntityFilter<Enemy, EnemySystem.Filter>, ISignalOnStageReset, ISignalOnTryLiquidSplash, ISignalOnBeforeInteraction,
        ISignalOnEnemyDespawned, ISignalOnEnemyRespawned, ISignalOnMarioPlayerMegaMushroomFootstep {
        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public Enemy* Enemy;
            public PhysicsCollider2D* Collider;
        }

        public override void OnInit(Frame f) {
            f.Context.PlayerOnlyMask = f.Layers.GetLayerMask("Player");
            f.Context.CircleRadiusTwo = Shape2D.CreateCircle(2);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var enemy = filter.Enemy;

            // handle respawning
            if (enemy->RespawnTimer > 0 && !enemy->DisableRespawning) {
                HandleDelayedRespawn(f, ref filter, stage);
            }

            if (!enemy->IsActive) {
                return;
            }

            var transform = filter.Transform;
            var collider = filter.Collider;

            QuantumUtils.Decrement(ref enemy->IntangibilityFrames);

            // Despawn off bottom of stage
            if (transform->Position.Y + collider->Shape.Box.Extents.Y + collider->Shape.Centroid.Y < stage.StageWorldMin.Y) {
                enemy->IsActive = false;
                enemy->IsDead = true;
                if (!enemy->DisableRespawning) {
                    enemy->SetDelayedRespawn();
                }
                if (f.Unsafe.TryGetPointer(filter.Entity, out PhysicsObject* physicsObject)) {
                    physicsObject->IsFrozen = true;
                }

                f.Signals.OnEnemyDespawned(filter.Entity);
                return;
            }

            // ignore offscreen should get reset when the enemy is killed
            if (enemy->StayAtHomeWhenOffscreen && !enemy->IgnoreOffscreen && !enemy->IsDead) {
                OffscreenCheck(f, ref filter, stage);
            }
        }

        public static void EnemyBumpTurnaround(Frame f, EntityRef entityA, EntityRef entityB) {
            EnemyBumpTurnaround(f, entityA, entityB, true);
        }

        public static void EnemyBumpTurnaroundOnlyFirst(Frame f, EntityRef entityA, EntityRef entityB) {
            EnemyBumpTurnaround(f, entityA, entityB, false);
        }

        public static void EnemyBumpTurnaround(Frame f, EntityRef entityA, EntityRef entityB, bool turnBoth) {
            var enemyA = f.Unsafe.GetPointer<Enemy>(entityA);
            var transformA = f.Unsafe.GetPointer<Transform2D>(entityA);
            var transformB = f.Unsafe.GetPointer<Transform2D>(entityB);

            QuantumUtils.UnwrapWorldLocations(f, transformA->Position, transformB->Position, out var ourPos, out var theirPos);
            bool right = ourPos.X > theirPos.X;
            if (ourPos.X == theirPos.X) {
                right = ourPos.Y < theirPos.Y;
            }
            enemyA->ChangeFacingRight(f, entityA, right);

            if (turnBoth) {
                var enemyB = f.Unsafe.GetPointer<Enemy>(entityB);
                enemyB->ChangeFacingRight(f, entityB, !right);
            }
        }

        public void HandleDelayedRespawn(Frame f, ref Filter filter, VersusStageData stage) {
            var enemy = filter.Enemy;
            if (QuantumUtils.Decrement(ref enemy->RespawnTimer)) {
                enemy->IntangibilityFrames = 30; // 30 / 60 is .50 for .50 seconds
                enemy->Respawn(f, filter.Entity);
                f.Events.EnemyAfterDelayedRespawn(filter.Entity);

            } else if (enemy->RespawnTimer == enemy->RespawnSparklesTimer && enemy->RespawnSparklesTimer != 0) {
                filter.Transform->Teleport(f, enemy->Spawnpoint);
                f.Events.EnemyPreRespawned(filter.Entity);
            }
        }

        public void OffscreenCheck(Frame f, ref Filter filter, VersusStageData stage) {
            var enemy = filter.Enemy;
            var allPlayersFilter = f.Filter<MarioPlayer, Transform2D>();
            var transform = filter.Transform;
            var entity = filter.Entity;

            bool marioInSpawnpoint = false;

            // check if any Mario is near the enemy
            allPlayersFilter.UseCulling = false;
            while (allPlayersFilter.NextUnsafe(out _, out _, out Transform2D* marioTransform)) {
                // despawn if out of view of Mario
                QuantumUtils.WrappedDistance(stage, transform->Position, marioTransform->Position, out FP distanceToMario);
                if (FPMath.Abs(distanceToMario) < Constants.EnemyMaxDistFromMario) {
                    return;
                }

                // check if a Mario is in the spawnpoint
                QuantumUtils.WrappedDistance(stage, enemy->Spawnpoint, marioTransform->Position, out FP marioDistToSpawnpoint);
                if (FPMath.Abs(marioDistToSpawnpoint) < Constants.EnemyHomeBoxBuffer) {
                    marioInSpawnpoint = true;
                }
            }

            bool foundPhysicsObj = f.Unsafe.TryGetPointer(filter.Entity, out PhysicsObject* physicsObject);

            // freeze check! Do not teleport frozen enemies.
            if (foundPhysicsObj && physicsObject->IsFrozen) {
                return;
            }

            if (!marioInSpawnpoint) {
                // set to 0
                if (foundPhysicsObj) {
                    physicsObject->Velocity = FPVector2.Zero;
                }

                // turn to face the player while in the shadows
                var shouldFaceRight = false;
                var closestMario = QuantumUtils.FindClosestAliveMario(f, enemy->Spawnpoint, out FPVector2 closestMarioPosition, stage);

                // use closest player and face them
                if (closestMario != EntityRef.None) {
                    QuantumUtils.WrappedDistance(f, enemy->Spawnpoint, closestMarioPosition, out FP xDiff);
                    shouldFaceRight = xDiff < 0;
                }

                enemy->FacingRight = shouldFaceRight;

                // only cause the despawn fart cloud and call the event if the enemy is not at its home
                QuantumUtils.WrappedDistance(f, transform->Position, enemy->Spawnpoint, out FP xEnemyDiffSpawnPoint);
                if (FPMath.Abs(xEnemyDiffSpawnPoint) > FP._0_25 && FPMath.Abs(transform->Position.Y - enemy->Spawnpoint.Y) > FP._0_50) {
                    f.Events.EnemyDespawnedOffscreen(entity, filter.Transform->Position);
                }

                transform->Teleport(f, enemy->Spawnpoint);
                f.Signals.OnEnemyReturnedHome(entity);
            }
        }

        public void OnStageReset(Frame f, QBoolean full) {
            var filter = f.Filter<Enemy, Transform2D>();
            while (filter.NextUnsafe(out EntityRef entity, out Enemy* enemy, out Transform2D* transform)) {
                if (full) {
                    // Check for respawns
                    if (enemy->DisableRespawning) {
                        continue;
                    }

                    if (!enemy->IgnorePlayerWhenRespawning) {
                        Physics2D.HitCollection playerHits = f.Physics2D.OverlapShape(enemy->Spawnpoint, 0, f.Context.CircleRadiusTwo, f.Context.PlayerOnlyMask);
                        if (playerHits.Count > 0) {
                            continue;
                        }
                    }

                    enemy->Respawn(f, entity);
                }

                if (enemy->IsActive) {
                    // Check for respawning blocks killing us
                    if (!f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)
                        || physicsObject->DisableCollision
                        || !f.Unsafe.TryGetPointer(entity, out PhysicsCollider2D* collider)) {
                        continue;
                    }

                    if (PhysicsObjectSystem.BoxInGround(f, transform->Position, collider->Shape, entity: entity)) {
                        f.Signals.OnEnemyKilledByStageReset(entity);
                    }
                }
            }
        }

        public void OnTryLiquidSplash(Frame f, EntityRef entity, EntityRef liquid, QBoolean exit, bool* doSplash) {
            if (f.Unsafe.TryGetPointer(entity, out Enemy* enemy)) {
                *doSplash &= enemy->IsActive;
            }
        }

        public void OnBeforeInteraction(Frame f, EntityRef entity, bool* allowInteraction) {
            if (f.Unsafe.TryGetPointer(entity, out Enemy* enemy)) {
                *allowInteraction &= enemy->IsAlive;
            }
        }

        public void OnEnemyDespawned(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out PhysicsCollider2D* collider)) {
                collider->Enabled = false;
            }
        }

        public void OnEnemyRespawned(Frame f, EntityRef entity) {
            if (f.Unsafe.TryGetPointer(entity, out PhysicsCollider2D* collider)) {
                collider->Enabled = true;
            }
        }

        public void OnMarioPlayerMegaMushroomFootstep(Frame f) {
            var it = f.Unsafe.FilterStruct<Filter>();
            Filter filter = default;
            while (it.Next(&filter)) {
                if (!filter.Enemy->IsAlive
                    || !f.Unsafe.TryGetPointer(filter.Entity, out PhysicsObject* physicsObject)
                    || physicsObject->IsFrozen
                    || physicsObject->DisableCollision
                    || !physicsObject->IsTouchingGround) {
                    continue;
                }
                
                physicsObject->Velocity.Y = Constants._3_50;
                physicsObject->IsTouchingGround = false;
            }
        }
    }
}