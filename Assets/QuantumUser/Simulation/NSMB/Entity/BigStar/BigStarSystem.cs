using Photon.Deterministic;
using Quantum.Physics2D;

namespace Quantum {
    public unsafe class BigStarSystem : SystemMainThread, ISignalOnTrigger2D {

        public override void Update(Frame f) {
            if (!f.Exists(f.Global->MainBigStar) && QuantumUtils.Decrement(ref f.Global->BigStarSpawnTimer)) {
                var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
                int spawnpoints = stage.BigStarSpawnpoints.Length;
                ref BitSet64 usedSpawnpoints = ref f.Global->UsedStarSpawns;
                for (int i = 0; i < spawnpoints; i++) {
                    // Find a spot...
                    if (f.Global->UsedStarSpawnCount == spawnpoints) {
                        usedSpawnpoints.ClearAll();
                        f.Global->UsedStarSpawnCount = 0;
                    }

                    int count = f.RNG->Next(0, spawnpoints - f.Global->UsedStarSpawnCount);
                    int index = 0;
                    for (int j = 0; j < spawnpoints; j++) {
                        if (!usedSpawnpoints.IsSet(j) && --count == 0) {
                            // This is the index to use
                            index = j;
                            break;
                        }
                    }
                    usedSpawnpoints.Set(index);
                    f.Global->UsedStarSpawnCount++;

                    // Spawn a star.
                    FPVector2 position = stage.BigStarSpawnpoints[index];
                    Shape2D shape = Shape2D.CreateCircle(FP.FromString("2.5"));
                    HitCollection hits = f.Physics2D.OverlapShape(position, 0, shape, f.Layers.GetLayerMask("Player"));

                    if (hits.Count == 0) {
                        // Hit no players
                        EntityRef newEntity = f.Create(f.SimulationConfig.BigStarPrototype);
                        f.Global->MainBigStar = newEntity;
                        var newStarTransform = f.Unsafe.GetPointer<Transform2D>(newEntity);
                        var newStar = f.Unsafe.GetPointer<BigStar>(newEntity);

                        newStarTransform->Position = position;
                        newStar->IsStationary = true;
                        break;
                    }
                }

                f.Global->BigStarSpawnTimer = f.Exists(f.Global->MainBigStar) ? (624 - (f.PlayerConnectedCount * 12)) : 30;
            }

            var allStars = f.Filter<BigStar>();
            while (allStars.NextUnsafe(out EntityRef entity, out BigStar* bigStar)) {
                HandleStar(f, entity, bigStar);
            }
        }

        private void HandleStar(Frame f, EntityRef entity, BigStar* bigStar) {
            if (!bigStar->IsStationary && QuantumUtils.Decrement(ref bigStar->Lifetime)) {
                f.Destroy(entity);
                return;
            }

            if (bigStar->IsStationary) {
                var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);
                if (physicsObject->IsTouchingGround) {
                    physicsObject->Velocity.Y = bigStar->BounceForce;
                }

                if (physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall) {
                    bigStar->FacingRight = physicsObject->IsTouchingLeftWall;
                    physicsObject->Velocity.X = bigStar->Speed * (bigStar->FacingRight ? 1 : -1);
                }
            }

        }

        public void OnTrigger2D(Frame f, TriggerInfo2D info) {
            if (f.DestroyPending(info.Other) ||
                !f.Unsafe.TryGetPointer(info.Entity, out MarioPlayer* mario) ||
                !f.Unsafe.TryGetPointer(info.Other, out BigStar* star)) {
                return;
            }

            if (mario->IsDead) {
                return;
            }

            mario->Stars++;
            f.Events.MarioPlayerCollectedStar(f, info.Entity, *mario);
            f.Destroy(info.Other);
        }
    }
}