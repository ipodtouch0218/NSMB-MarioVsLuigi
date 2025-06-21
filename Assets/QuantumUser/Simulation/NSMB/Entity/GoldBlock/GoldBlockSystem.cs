using Photon.Deterministic;
using static UnityEngine.EventSystems.EventTrigger;

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
            var entity = filter.Entity;
            var goldBlock = filter.GoldBlock;
            var transform = filter.Transform;

            if (f.Exists(goldBlock->AttachedTo)) {
                // Attached to a player.
                var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(goldBlock->AttachedTo);
                
                if (FPMath.Abs(marioPhysicsObject->Velocity.X) > 5) {
                    goldBlock->Timer += 10;
                } else if (marioPhysicsObject->Velocity.SqrMagnitude > FP._0_05) {
                    goldBlock->Timer++;
                }

                if (goldBlock->Timer >= 40) {
                    var mario = f.Unsafe.GetPointer<MarioPlayer>(goldBlock->AttachedTo);
                    mario->GamemodeData.CoinRunners->ObjectiveCoins++;
                    f.Events.GoldBlockGeneratedObjectiveCoin(entity);
                    f.Events.MarioPlayerObjectiveCoinsChanged(goldBlock->AttachedTo);
                    goldBlock->Timer = 0;
                    if (--goldBlock->ObjectiveCoinsRemaining == 0) {
                        f.Events.GoldBlockRanOutOfCoins(entity);
                        f.Destroy(entity);
                        return;
                    }
                }
                transform->Position = f.Unsafe.GetPointer<Transform2D>(goldBlock->AttachedTo)->Position + FPVector2.Up;
            } else {
                // Slowly float downwards
                var coinItem = f.Unsafe.GetPointer<CoinItem>(entity);
                f.Unsafe.GetPointer<PhysicsCollider2D>(entity)->Enabled = coinItem->SpawnAnimationFrames == 0;
                if (coinItem->SpawnAnimationFrames > 0) {
                    return;
                }

                var platform = f.Unsafe.GetPointer<MovingPlatform>(entity);
                var collider = f.Unsafe.GetPointer<PhysicsCollider2D>(entity);
                FP targetVel;

                bool closeToGround;
                if (PhysicsObjectSystem.BoxInGround((FrameThreadSafe) f, transform->Position, collider->Shape, stage: stage, entity: entity)) {
                    closeToGround = false;
                } else {
                    closeToGround = (transform->Position.Y - stage.StageWorldMin.Y) <= 4
                        || PhysicsObjectSystem.Raycast((FrameThreadSafe) f, stage, transform->Position + (FPVector2.Left / 4), FPVector2.Down, 2, out _)
                        || PhysicsObjectSystem.Raycast((FrameThreadSafe) f, stage, transform->Position + (FPVector2.Right / 4), FPVector2.Down, 2, out _);
                }

                if (closeToGround) {
                    // Go upwards
                    targetVel = Constants._2_50;
                } else {
                    // Float downwards
                    targetVel = -Constants._2_50;
                }

                platform->Velocity.Y = QuantumUtils.MoveTowards(platform->Velocity.Y, targetVel, 7 * f.DeltaTime);
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
                ObjectiveCoinSystem.SpawnObjectiveCoins(f, transform->Position, 10, 0, false);
                f.Events.GoldBlockBrokenByMega(contact.Entity);
                f.Destroy(contact.Entity);
                keepContacts = false;
                return;
            }
            if (f.Unsafe.TryGetPointer(goldBlockEntity, out CoinItem* coinItem) && coinItem->SpawnAnimationFrames > 0) {
                keepContacts = false;
                return;
            }

            // If we hit from below, cancel contacts and equip to player.
            if (FPVector2.Dot(contact.Normal, FPVector2.Down) >= PhysicsObjectSystem.GroundMaxAngle
                || (mario->IsGroundpoundActive && FPVector2.Dot(contact.Normal, FPVector2.Up) >= PhysicsObjectSystem.GroundMaxAngle)) {
                // Hit from below.

                bool handled = false;
                foreach ((var otherGoldBlockEntity, var otherGoldBlock) in f.Unsafe.GetComponentBlockIterator<GoldBlock>()) {
                    if (otherGoldBlock->AttachedTo == marioEntity) {
                        // Already wearing a gold block. Refresh this one, instead.
                        otherGoldBlock->ObjectiveCoinsRemaining += GetCoinsInGoldBlock(f, mario);
                        f.Events.MarioPlayerPickedUpGoldBlock(marioEntity, otherGoldBlockEntity);
                        handled = true;
                        break;
                    }
                }

                if (!handled) {
                    goldBlock->AttachedTo = marioEntity;
                    goldBlock->ObjectiveCoinsRemaining = GetCoinsInGoldBlock(f, mario);
                    //f.Unsafe.GetPointer<CoinItem>(helmetEntity)->Lifetime = 0;
                    f.Remove<CoinItem>(goldBlockEntity);
                    f.Remove<PhysicsCollider2D>(goldBlockEntity);
                    f.Remove<MovingPlatform>(goldBlockEntity);
                    f.Events.MarioPlayerPickedUpGoldBlock(marioEntity, goldBlockEntity);
                } else {
                    f.Destroy(goldBlockEntity);
                }
                keepContacts = mario->IsGroundpoundActive;
            }
        }

        public void OnMarioPlayerDied(Frame f, EntityRef entity) {
            foreach ((EntityRef goldBlockEntity, var goldBlock) in f.Unsafe.GetComponentBlockIterator<GoldBlock>()) {
                if (goldBlock->AttachedTo == entity) {
                    f.Events.GoldBlockLostViaDamage(goldBlockEntity);
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
                    f.Destroy(goldBlockEntity);
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

        private static int GetCoinsInGoldBlock(Frame f, MarioPlayer* mario) {
            var gamemode = f.FindAsset(f.Global->Rules.Gamemode);
            int firstPlaceCoins = gamemode.GetFirstPlaceObjectiveCount(f);
            return 25 + (firstPlaceCoins - mario->GamemodeData.CoinRunners->ObjectiveCoins) / 3;
        }
    }
}
