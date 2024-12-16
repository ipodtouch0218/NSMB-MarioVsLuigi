namespace Quantum {

    public unsafe class EnemySystem : SystemMainThreadFilterStage<EnemySystem.Filter>, ISignalOnStageReset, ISignalOnTryLiquidSplash, ISignalOnBeforeInteraction,
        ISignalOnEnemyDespawned, ISignalOnEnemyRespawned {
        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public Enemy* Enemy;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* Collider;
        }

        public override void OnInit(Frame f) {
            f.Context.PlayerOnlyMask = f.Layers.GetLayerMask("Player");
            f.Context.CircleRadiusTwo = Shape2D.CreateCircle(2);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var enemy = filter.Enemy;
            var transform = filter.Transform;
            var physicsObject = filter.PhysicsObject;
            var collider = filter.Collider;

            if (!enemy->IsActive) {
                return;
            }

            // Despawn off bottom of stage
            if (transform->Position.Y + collider->Shape.Box.Extents.Y + collider->Shape.Centroid.Y < stage.StageWorldMin.Y) {
                enemy->IsActive = false;
                enemy->IsDead = true;
                physicsObject->IsFrozen = true;

                f.Signals.OnEnemyDespawned(filter.Entity);
                return;
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
            var enemyB = f.Unsafe.GetPointer<Enemy>(entityB);
            var transformA = f.Unsafe.GetPointer<Transform2D>(entityA);
            var transformB = f.Unsafe.GetPointer<Transform2D>(entityB);

            QuantumUtils.UnwrapWorldLocations(f, transformA->Position, transformB->Position, out var ourPos, out var theirPos);
            bool right = ourPos.X > theirPos.X;
            if (ourPos.X == theirPos.X) {
                right = ourPos.Y < theirPos.Y;
            }
            enemyA->ChangeFacingRight(f, entityA, right);
            if (turnBoth) {
                enemyB->ChangeFacingRight(f, entityB, !right);
            }
        }

        public void OnStageReset(Frame f, QBoolean full) {
            var filter = f.Filter<Enemy, Transform2D>();

            while (filter.NextUnsafe(out EntityRef entity, out Enemy* enemy, out Transform2D* transform)) {
                if (enemy->IsActive) {
                    // Check for respawning blocks killing us
                    if (!f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)
                        || physicsObject->DisableCollision) {
                        continue;
                    }
                    if (!f.Unsafe.TryGetPointer(entity, out PhysicsCollider2D* collider)) {
                        continue;
                    }

                    if (PhysicsObjectSystem.BoxInGround((FrameThreadSafe) f, transform->Position, collider->Shape, entity: entity)) {
                        f.Signals.OnEnemyKilledByStageReset(entity);
                    }
                } else {
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
                    f.Signals.OnEnemyRespawned(entity);
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
            if (f.Has<Enemy>(entity) && f.Unsafe.TryGetPointer(entity, out PhysicsCollider2D* collider)) {
                collider->Enabled = false;
            }
        }

        public void OnEnemyRespawned(Frame f, EntityRef entity) {
            if (f.Has<Enemy>(entity) && f.Unsafe.TryGetPointer(entity, out PhysicsCollider2D* collider)) {
                collider->Enabled = true;

            }
        }
    }
}