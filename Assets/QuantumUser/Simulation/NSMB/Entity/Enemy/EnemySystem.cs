using System;
using System.Collections.Generic;

namespace Quantum {

    public unsafe class EnemySystem : SystemMainThreadFilterStage<EnemySystem.Filter>, ISignalOnStageReset, ISignalOnTryLiquidSplash {

        public delegate void EnemyInteractor(Frame f, EntityRef firstEntity, EntityRef secondEntity);
        private static readonly Dictionary<(Type, Type), EnemyInteractor> interactors = new();
        private static readonly HashSet<(EntityRef, EntityRef)> alreadyCollided = new(new UnorderedTupleEqualityComparer<EntityRef>());

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public Enemy* Enemy;
            public PhysicsObject* PhysicsObject;
            public PhysicsCollider2D* Collider;
        }

        public override void BeforeUpdate(Frame f, VersusStageData stage) {
            alreadyCollided.Clear();
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

            // Collide
            if (enemy->IsAlive && !enemy->ColliderDisabled) {
                var hits = f.Physics2D.OverlapShape(*transform, filter.Collider->Shape);
                EntityRef entityA = filter.Entity;
                for (int i = 0; i < hits.Count; i++) {
                    EntityRef entityB = hits[i].Entity;
                    var entities = (entityA, entityB);
                    if (entityA == entityB
                        || alreadyCollided.Contains(entities)
                        || (f.TryGet(entityB, out Enemy entityBEnemy) && (!entityBEnemy.IsAlive || entityBEnemy.ColliderDisabled))
                        || (f.TryGet(entityB, out MarioPlayer entityBMario) && entityBMario.IsDead)) {
                        continue;
                    }

                    foreach ((var key, var interactor) in interactors) {
                        int componentIdA = ComponentTypeId.GetComponentIndex(key.Item1);
                        int componentIdB = ComponentTypeId.GetComponentIndex(key.Item2);

                        if (f.Has(entityA, componentIdA)
                            && f.Has(entityB, componentIdB)) {

                            interactor(f, entityA, entityB);
                            alreadyCollided.Add(entities);
                            break;

                        } else if (f.Has(entityB, componentIdA)
                            && f.Has(entityA, componentIdB)) {

                            interactor(f, entityB, entityA);
                            alreadyCollided.Add(entities);
                            break;
                        }
                    }
                }
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
            enemyA->FacingRight = right;
            if (turnBoth) {
                enemyB->FacingRight = !right;
            }
        }

        public static void RegisterInteraction<X, Y>(EnemyInteractor interactor) where X : unmanaged, IComponent where Y : unmanaged, IComponent {
            var key = (typeof(X), typeof(Y));

            if (interactors.ContainsKey(key)) {
                Log.Error($"Already registered an interactor between {typeof(X).Name} and {typeof(Y).Name}");
            } else {
                interactors[key] = interactor;
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