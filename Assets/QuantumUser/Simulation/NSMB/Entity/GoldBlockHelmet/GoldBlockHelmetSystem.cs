using Photon.Deterministic;

namespace Quantum {
    public unsafe class GoldBlockHelmetSystem : SystemMainThreadEntityFilter<GoldBlockHelmet, GoldBlockHelmetSystem.Filter>,
        ISignalOnStageReset, ISignalOnMarioPlayerDied, ISignalOnMarioPlayerTakeDamage {

        public override bool StartEnabled => false;

        public struct Filter {
            public EntityRef Entity;
            public GoldBlockHelmet* Helmet;
            public Transform2D* Transform;
        }

        public override void OnInit(Frame f) {
            f.Context.RegisterPreContactCallback(f, OnMarioGoldBlockHelmetPreContact);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var helmet = filter.Helmet;
            if (f.Exists(helmet->AttachedTo)) {
                // Attached to a player.
                var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(helmet->AttachedTo);
                
                if (FPMath.Abs(marioPhysicsObject->Velocity.X) > 5) {
                    helmet->Timer += 10;
                } else if (marioPhysicsObject->Velocity.SqrMagnitude > FP._0_25) {
                    helmet->Timer++;
                }

                if (helmet->Timer >= 45) {
                    var mario = f.Unsafe.GetPointer<MarioPlayer>(helmet->AttachedTo);
                    mario->GamemodeData.CoinRunners->ObjectiveCoins++;
                    f.Events.GoldBlockHelmetGeneratedObjectiveCoin(filter.Entity);
                    f.Events.MarioPlayerObjectiveCoinsChanged(helmet->AttachedTo);
                    helmet->Timer = 0;
                    if (--helmet->ObjectiveCoinsRemaining == 0) {
                        f.Destroy(filter.Entity);
                        return;
                    }
                }
                filter.Transform->Position = f.Unsafe.GetPointer<Transform2D>(helmet->AttachedTo)->Position + FPVector2.Up;
            } else {
                // Slowly float downwards
                var platform = f.Unsafe.GetPointer<MovingPlatform>(filter.Entity);
                FP targetVel;

                bool closeToGround = PhysicsObjectSystem.Raycast((FrameThreadSafe) f, stage, filter.Transform->Position + (FPVector2.Left / 3), FPVector2.Down, 3, out _)
                    || PhysicsObjectSystem.Raycast((FrameThreadSafe) f, stage, filter.Transform->Position + (FPVector2.Right / 3), FPVector2.Down, 3, out _);

                if (closeToGround) {
                    // Go upwards
                    targetVel = 2;
                } else {
                    // Float downwards
                    targetVel = -2;
                }

                platform->Velocity.Y = QuantumUtils.MoveTowards(platform->Velocity.Y, targetVel, 10 * f.DeltaTime);
            }
        }

        public void OnStageReset(Frame f, QBoolean full) {
            VersusStageData stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            var filter = f.Filter<GoldBlockHelmet, Transform2D, PhysicsCollider2D>();
            while (filter.NextUnsafe(out EntityRef entity, out GoldBlockHelmet* helmet, out Transform2D* transform, out PhysicsCollider2D* collider)) {
                if (f.Exists(helmet->AttachedTo)) {
                    return;
                }

                if (PhysicsObjectSystem.BoxInGround((FrameThreadSafe) f, transform->Position, collider->Shape, stage: stage, entity: entity)) {
                    f.Events.CollectableDespawned(entity, transform->Position, false);
                    f.Destroy(entity);
                }
            }
        }

        private static void OnMarioGoldBlockHelmetPreContact(Frame f, VersusStageData stage, EntityRef marioEntity, PhysicsContact contact, ref bool keepContacts) {
            EntityRef helmetEntity = contact.Entity;
            if (!f.Unsafe.TryGetPointer(marioEntity, out MarioPlayer* mario) || !f.Unsafe.TryGetPointer(helmetEntity, out GoldBlockHelmet* helmet)) {
                return;
            }

            var existingHelmets = f.Filter<GoldBlockHelmet>();
            while (existingHelmets.NextUnsafe(out _, out GoldBlockHelmet* otherHelmet)) {
                if (otherHelmet->AttachedTo == marioEntity) {
                    // Already wearing a helmet. Don't allow multiple.
                    return;
                }
            }

            // If we hit from below, cancel contacts and equip to player.
            if (FPVector2.Dot(contact.Normal, FPVector2.Down) >= PhysicsObjectSystem.GroundMaxAngle
                || (mario->IsGroundpoundActive && FPVector2.Dot(contact.Normal, FPVector2.Up) >= PhysicsObjectSystem.GroundMaxAngle)) {

                // Hit from below.
                helmet->AttachedTo = marioEntity;
                helmet->ObjectiveCoinsRemaining = 50;
                //f.Unsafe.GetPointer<CoinItem>(helmetEntity)->Lifetime = 0;
                f.Remove<CoinItem>(helmetEntity);
                f.Remove<PhysicsCollider2D>(helmetEntity);
                f.Remove<MovingPlatform>(helmetEntity);
                f.Events.MarioPlayerPickedUpGoldBlockHelmet(marioEntity, helmetEntity);

                keepContacts = mario->IsGroundpoundActive;
            }
        }

        public void OnMarioPlayerDied(Frame f, EntityRef entity) {
            var filter = f.Filter<GoldBlockHelmet>();
            while (filter.NextUnsafe(out var helmetEntity, out var helmet)) {
                if (helmet->AttachedTo == entity) {
                    f.Destroy(helmetEntity);
                }
            }
        }

        public void OnMarioPlayerTakeDamage(Frame f, EntityRef entity, ref QBoolean keepDamage) {
            var helmets = f.Filter<GoldBlockHelmet>();
            while (helmets.NextUnsafe(out var helmetEntity, out var helmet)) {
                if (helmet->AttachedTo == entity) {
                    keepDamage = false;
                    f.Unsafe.GetPointer<MarioPlayer>(entity)->DamageInvincibilityFrames = 90;
                    f.Destroy(helmetEntity);
                    f.Events.MarioPlayerLostGoldBlockHelmet(entity);
                    return;
                }
            }
        }
    }
}
