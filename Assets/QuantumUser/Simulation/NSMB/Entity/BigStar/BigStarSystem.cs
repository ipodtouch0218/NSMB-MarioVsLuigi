using Photon.Deterministic;
using Quantum.Physics2D;

namespace Quantum {
    public unsafe class BigStarSystem : SystemMainThread, ISignalOnReturnToRoom, ISignalOnMarioPlayerDropObjective {

        public override bool StartEnabled => false;

        public override void OnInit(Frame f) {
            f.Context.Interactions.Register<BigStar, MarioPlayer>(f, OnBigStarMarioInteraction);
        }

        public override void Update(Frame f) {
            VersusStageData stage = null;

            if (!f.Exists(f.Global->MainBigStar) && QuantumUtils.Decrement(ref f.Global->BigStarSpawnTimer)) {
                stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
                HandleSpawningNewStar(f, stage);
            }

            var allStars = f.Filter<BigStar>();
            while (allStars.NextUnsafe(out EntityRef entity, out BigStar* bigStar)) {
                HandleStar(f, ref stage, entity, bigStar);
            }
        }

        private void HandleSpawningNewStar(Frame f, VersusStageData stage) {
            int spawnpoints = stage.BigStarSpawnpoints.Length;
            ref BitSet64 usedSpawnpoints = ref f.Global->UsedStarSpawns;

            bool spawnedStar = false;
            for (int i = 0; i < spawnpoints; i++) {
                // Find a spot...
                if (f.Global->UsedStarSpawnCount >= spawnpoints) {
                    usedSpawnpoints.ClearAll();
                    f.Global->UsedStarSpawnCount = 0;
                }

                int count = f.RNG->Next(0, spawnpoints - f.Global->UsedStarSpawnCount);
                int index = 0;
                for (int j = 0; j < spawnpoints; j++) {
                    if (!usedSpawnpoints.IsSet(j)) {
                        if (count-- == 0) {
                            // This is the index to use
                            index = j;
                            break;
                        }
                    }
                }
                usedSpawnpoints.Set(index);
                f.Global->UsedStarSpawnCount++;

                // Spawn a star.
                FPVector2 position = stage.BigStarSpawnpoints[index];
                HitCollection hits = f.Physics2D.OverlapShape(position, 0, f.Context.CircleRadiusTwo, f.Context.PlayerOnlyMask);

                if (hits.Count == 0) {
                    // Hit no players
                    var gamemode = f.FindAsset(f.Global->Rules.Gamemode) as StarChasersGamemode;
                    EntityRef newEntity = f.Create(gamemode.BigStarPrototype);
                    f.Global->MainBigStar = newEntity;
                    var newStarTransform = f.Unsafe.GetPointer<Transform2D>(newEntity);
                    var newStar = f.Unsafe.GetPointer<BigStar>(newEntity);
                    var newStarPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(newEntity);

                    newStarTransform->Position = position;
                    newStar->IsStationary = true;
                    newStarPhysicsObject->DisableCollision = true;
                    spawnedStar = true;
                    break;
                }
            }

            if (!spawnedStar) {
                f.Global->BigStarSpawnTimer = 30;
            }
        }

        private void HandleStar(Frame f, ref VersusStageData stage, EntityRef entity, BigStar* bigStar) {
            if (bigStar->IsStationary) {
                return;
            }

            if (stage == null) {
                stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            }

            var transform = f.Unsafe.GetPointer<Transform2D>(entity);
            if (QuantumUtils.Decrement(ref bigStar->Lifetime) || (transform->Position.Y < stage.StageWorldMin.Y && bigStar->UncollectableFrames == 0)) {
                f.Events.CollectableDespawned(entity, transform->Position, false);
                f.Destroy(entity);
                return;
            }

            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);
            if (physicsObject->IsTouchingGround) {
                physicsObject->Velocity.Y = bigStar->BounceForce;
                physicsObject->IsTouchingGround = false;
            }

            if (!stage.IsWrappingLevel) {
                var physicsCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(entity);
                ref Shape2D shape = ref physicsCollider->Shape;
                if (transform->Position.X - shape.Centroid.X - shape.Box.Extents.X <= stage.StageWorldMin.X) {
                    // Hit left wall
                    bigStar->FacingRight = true;
                    physicsObject->Velocity.X = bigStar->Speed;
                } else if (transform->Position.X + shape.Centroid.X + shape.Box.Extents.X >= stage.StageWorldMax.X) {
                    // Hit right wall
                    bigStar->FacingRight = false;
                    physicsObject->Velocity.X = -bigStar->Speed;
                }
            }

            if (physicsObject->IsTouchingLeftWall || physicsObject->IsTouchingRightWall) {
                bigStar->FacingRight = physicsObject->IsTouchingLeftWall;
                physicsObject->Velocity.X = bigStar->Speed * (bigStar->FacingRight ? 1 : -1);
            }

            if (physicsObject->DisableCollision && QuantumUtils.Decrement(ref bigStar->UncollectableFrames) && transform->Position.Y < FPMath.Max(stage.StageWorldMin.Y + 7, stage.StageWorldMax.Y)) {
                var physicsCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(entity);
                if (!PhysicsObjectSystem.BoxInGround((FrameThreadSafe) f, transform->Position, physicsCollider->Shape, true, stage)) {
                    physicsObject->DisableCollision = false;
                }
            }
        }

        public void OnBigStarMarioInteraction(Frame f, EntityRef starEntity, EntityRef marioEntity) {
            if (!f.Exists(starEntity) || f.DestroyPending(starEntity)) {
                return;
            }

            var star = f.Unsafe.GetPointer<BigStar>(starEntity);
            if (star->UncollectableFrames > 0) {
                return;
            }

            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            if (mario->IsDead) {
                return;
            }

            mario->GamemodeData.StarChasers->Stars++;
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);

            if (star->IsStationary) {
                stage.ResetStage(f, false);
                f.Global->BigStarSpawnTimer = (ushort) (624 - (f.Global->RealPlayers * 12));
            }

            f.Signals.OnMarioPlayerCollectedStar(marioEntity);
            GameLogicSystem.CheckForGameEnd(f);

            f.Events.MarioPlayerCollectedStar(marioEntity, *mario, f.Unsafe.GetPointer<Transform2D>(starEntity)->Position);
            f.Events.CollectableDespawned(starEntity, f.Unsafe.GetPointer<Transform2D>(starEntity)->Position, true);
            f.Destroy(starEntity);
        }

        public void OnReturnToRoom(Frame f) {
            f.Global->MainBigStar = EntityRef.None;
            f.Global->BigStarSpawnTimer = 0;
            f.Global->UsedStarSpawnCount = 0;
            f.Global->UsedStarSpawns.ClearAll();
        }

        public void OnMarioPlayerDropObjective(Frame f, EntityRef entity, int amount, EntityRef attacker) {
            if (f.Unsafe.TryGetPointer(entity, out MarioPlayer* mario)) {
                mario->SpawnStars(f, entity, amount);
            }
        }
    }
}
