using Photon.Deterministic;

namespace Quantum {
    public unsafe class GoldBlockSystem : SystemMainThreadEntityFilter<GoldBlock, GoldBlockSystem.Filter>,
        ISignalOnStageReset, ISignalOnMarioPlayerDied, ISignalOnMarioPlayerTakeDamage, ISignalOnMarioPlayerCollectedPowerup {

        public override bool StartEnabled => false;

        public struct Filter {
            public EntityRef Entity;
            public GoldBlock* GoldBlock;
            public Transform2D* Transform;
        }

        public override void OnInit(Frame f) {
            f.Context.RegisterPreContactCallback(f, OnMarioGoldBlockHelmetPreContact);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var goldBlock = filter.GoldBlock;
            if (f.Exists(goldBlock->AttachedTo)) {
                // Attached to a player.
                if (goldBlock->DespawnTimer > 0) {
                    if (QuantumUtils.Decrement(ref goldBlock->DespawnTimer)) {
                        f.Destroy(filter.Entity);
                    }
                    return;
                }
                var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(goldBlock->AttachedTo);
                
                if (FPMath.Abs(marioPhysicsObject->Velocity.X) > 5) {
                    goldBlock->Timer += 10;
                } else if (marioPhysicsObject->Velocity.SqrMagnitude > FP._0_05) {
                    goldBlock->Timer++;
                }

                if (goldBlock->Timer >= 40) {
                    var mario = f.Unsafe.GetPointer<MarioPlayer>(goldBlock->AttachedTo);
                    mario->GamemodeData.CoinRunners->ObjectiveCoins++;
                    f.Events.GoldBlockGeneratedObjectiveCoin(filter.Entity);
                    f.Events.MarioPlayerObjectiveCoinsChanged(goldBlock->AttachedTo);
                    goldBlock->Timer = 0;
                    if (--goldBlock->ObjectiveCoinsRemaining == 0) {
                        f.Events.GoldBlockRanOutOfCoins(filter.Entity);
                        f.Destroy(filter.Entity);
                        return;
                    }
                }
                filter.Transform->Position = f.Unsafe.GetPointer<Transform2D>(goldBlock->AttachedTo)->Position + FPVector2.Up;
            } else {
                // Slowly float downwards
                var coinItem = f.Unsafe.GetPointer<CoinItem>(filter.Entity);
                f.Unsafe.GetPointer<PhysicsCollider2D>(filter.Entity)->Enabled = coinItem->SpawnAnimationFrames == 0;
                if (coinItem->SpawnAnimationFrames > 0) {
                    return;
                }

                var transform = filter.Transform;
                var platform = f.Unsafe.GetPointer<MovingPlatform>(filter.Entity);
                FP targetVel;

                bool closeToGround = (transform->Position.Y - stage.StageWorldMin.Y) <= 4
                    || PhysicsObjectSystem.Raycast((FrameThreadSafe) f, stage, transform->Position + (FPVector2.Left / 4), FPVector2.Down, 2, out PhysicsContact x)
                    || PhysicsObjectSystem.Raycast((FrameThreadSafe) f, stage, transform->Position + (FPVector2.Right / 4), FPVector2.Down, 2, out x);

                if (closeToGround) {
                    // Go upwards
                    targetVel = 3;
                } else {
                    // Float downwards
                    targetVel = -3;
                }

                platform->Velocity.Y = QuantumUtils.MoveTowards(platform->Velocity.Y, targetVel, 6 * f.DeltaTime);
            }
        }

        public void OnStageReset(Frame f, QBoolean full) {
            VersusStageData stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            var filter = f.Filter<GoldBlock, Transform2D, PhysicsCollider2D>();
            while (filter.NextUnsafe(out EntityRef entity, out GoldBlock* goldBlock, out Transform2D* transform, out PhysicsCollider2D* collider)) {
                if (f.Exists(goldBlock->AttachedTo)) {
                    return;
                }

                if (PhysicsObjectSystem.BoxInGround((FrameThreadSafe) f, transform->Position, collider->Shape, stage: stage, entity: entity)) {
                    f.Events.CollectableDespawned(entity, transform->Position, false);
                    f.Destroy(entity);
                }
            }
        }

        private static void OnMarioGoldBlockHelmetPreContact(Frame f, VersusStageData stage, EntityRef marioEntity, PhysicsContact contact, ref bool keepContacts) {
            EntityRef goldBlockEntity = contact.Entity;
            if (!f.Unsafe.TryGetPointer(marioEntity, out MarioPlayer* mario) || !f.Unsafe.TryGetPointer(goldBlockEntity, out GoldBlock* goldBlock)) {
                return;
            }
            if (mario->CurrentPowerupState == PowerupState.MegaMushroom) {
                // Break into 10 coins
                var transform = f.Unsafe.GetPointer<Transform2D>(contact.Entity);
                ObjectiveCoinSystem.SpawnObjectiveCoins(f, transform->Position, 10, 0);
                f.Events.GoldBlockBrokenByMega(contact.Entity);
                f.Destroy(contact.Entity);
                keepContacts = false;
                return;
            }
            if (f.Unsafe.TryGetPointer(goldBlockEntity, out CoinItem* coinItem) && coinItem->SpawnAnimationFrames > 0) {
                keepContacts = false;
                return;
            }

            var allGoldBlocks = f.Filter<GoldBlock>();
            while (allGoldBlocks.NextUnsafe(out _, out var otherGoldBlock)) {
                if (otherGoldBlock->AttachedTo == marioEntity) {
                    // Already wearing a gold block. Don't allow multiple.
                    return;
                }
            }

            // If we hit from below, cancel contacts and equip to player.
            if (FPVector2.Dot(contact.Normal, FPVector2.Down) >= PhysicsObjectSystem.GroundMaxAngle
                || (mario->IsGroundpoundActive && FPVector2.Dot(contact.Normal, FPVector2.Up) >= PhysicsObjectSystem.GroundMaxAngle)) {

                // Hit from below.
                goldBlock->AttachedTo = marioEntity;
                goldBlock->ObjectiveCoinsRemaining = 50;
                //f.Unsafe.GetPointer<CoinItem>(helmetEntity)->Lifetime = 0;
                f.Remove<CoinItem>(goldBlockEntity);
                f.Remove<PhysicsCollider2D>(goldBlockEntity);
                f.Remove<MovingPlatform>(goldBlockEntity);

                //f.Unsafe.GetPointer<PhysicsObject>(marioEntity)->Velocity.X = 0;

                f.Events.MarioPlayerPickedUpGoldBlock(marioEntity, goldBlockEntity);
                keepContacts = mario->IsGroundpoundActive;
            }
        }

        public void OnMarioPlayerDied(Frame f, EntityRef entity) {
            foreach ((EntityRef goldBlockEntity, var goldBlock) in f.Unsafe.GetComponentBlockIterator<GoldBlock>()) {
                if (goldBlock->AttachedTo == entity) {
                    f.Destroy(goldBlockEntity);
                }
            }
        }

        public void OnMarioPlayerTakeDamage(Frame f, EntityRef entity, ref QBoolean keepDamage) {
            foreach ((EntityRef goldBlockEntity, var goldBlock) in f.Unsafe.GetComponentBlockIterator<GoldBlock>()) {
                if (goldBlock->AttachedTo == entity) {
                    keepDamage = false;
                    f.Unsafe.GetPointer<MarioPlayer>(entity)->DamageInvincibilityFrames = 90;
                    f.Events.GoldBlockLostViaDamage(goldBlockEntity);
                    goldBlock->DespawnTimer = 60;
                    return;
                }
            }
        }

        public void OnMarioPlayerCollectedPowerup(Frame f, EntityRef marioEntity, EntityRef powerupEntity) {
            var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
            if (mario->CurrentPowerupState != PowerupState.MegaMushroom) {
                return;
            }

            // Break gold blocks for this player
            foreach ((EntityRef goldBlockEntity, var goldBlock) in f.Unsafe.GetComponentBlockIterator<GoldBlock>()) {
                if (goldBlock->AttachedTo == marioEntity) {
                    f.Events.GoldBlockLostViaDamage(goldBlockEntity);
                    f.Destroy(goldBlockEntity);
                }
            }
        }
    }
}
