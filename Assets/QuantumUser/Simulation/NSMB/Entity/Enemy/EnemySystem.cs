namespace Quantum {

    public unsafe class EnemySystem : SystemMainThreadFilterStage<EnemySystem.Filter>, ISignalOnStageReset, ISignalOnTryLiquidSplash {
        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public Enemy* Enemy;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* Collider;
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
            var transformA = f.Get<Transform2D>(entityA);
            var transformB = f.Get<Transform2D>(entityB);

            QuantumUtils.UnwrapWorldLocations(f, transformA.Position, transformB.Position, out var ourPos, out var theirPos);
            bool right = ourPos.X > theirPos.X;
            if (ourPos.X == theirPos.X) {
                right = ourPos.Y < theirPos.Y;
            }
            enemyA->FacingRight = right;
            if (turnBoth) {
                enemyB->FacingRight = !right;
            }
        }

        public void OnStageReset(Frame f, QBoolean full) {
            var filter = f.Filter<Enemy>();
            var shape = Shape2D.CreateCircle(2);
            var layerMask = f.Layers.GetLayerMask("Player");

            while (filter.NextUnsafe(out EntityRef entity, out Enemy* enemy)) {
                if (!enemy->IsActive) {
                    if (!enemy->IgnorePlayerWhenRespawning) {
                        Physics2D.HitCollection playerHits = f.Physics2D.OverlapShape(enemy->Spawnpoint, 0, shape, layerMask);
                        if (playerHits.Count > 0) {
                            continue;
                        }
                    }
                    
                    enemy->Respawn(f, entity);
                    f.Signals.OnEnemyRespawned(entity);
                }
            }
        }

        public void OnTryLiquidSplash(Frame f, EntityRef entity, EntityRef liquid, bool* doSplash) {
            if (!f.TryGet(entity, out Enemy enemy)) {
                *doSplash &= enemy.IsActive;
            }
        }
    }
}