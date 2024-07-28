namespace Quantum {

    public unsafe class EnemySystem : SystemMainThreadFilterStage<EnemySystem.Filter>, ISignalOnStageReset {
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

            // Inactive check
            if (!enemy->IsActive) {
                transform->Position = enemy->Spawnpoint;
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

        public void OnStageReset(Frame f, QBoolean full) {
            var filter = f.Filter<Transform2D, Enemy>();
            var shape = Shape2D.CreateCircle(2);
            var layerMask = f.Layers.GetLayerMask("Player");

            while (filter.NextUnsafe(out EntityRef entity, out Transform2D* transform, out Enemy* enemy)) {
                if (!enemy->IsActive) {
                    Physics2D.HitCollection playerHits = f.Physics2D.OverlapShape(*transform, shape, layerMask);
                    if (playerHits.Count == 0) {
                        enemy->Respawn(f, entity);
                        f.Signals.OnEnemyRespawned(entity);
                    }
                }
            }
        }
    }
}