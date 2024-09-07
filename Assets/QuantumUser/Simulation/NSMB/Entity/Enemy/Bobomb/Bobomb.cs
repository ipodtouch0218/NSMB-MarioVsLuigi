using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct Bobomb {

        public void Respawn(Frame f, EntityRef entity) {
            var holdable = f.Unsafe.GetPointer<Holdable>(entity);

            CurrentDetonationFrames = 0;
            holdable->Holder = default;
            holdable->PreviousHolder = default;
            holdable->IgnoreOwnerFrames = 0;
        }

        public void Kick(Frame f, EntityRef entity, EntityRef initiator, FP speed) {
            var holdable = f.Unsafe.GetPointer<Holdable>(entity);
            holdable->PreviousHolder = initiator;
            holdable->IgnoreOwnerFrames = 15;

            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);
            physicsObject->Velocity = new(
                FP.FromString("4.5") + speed,
                FP.FromString("3.5")
            );

            f.Events.PlayComboSound(f, entity, 0);
        }

        public void Kill(Frame f, EntityRef entity, EntityRef killerEntity, bool special) {
            var enemy = f.Unsafe.GetPointer<Enemy>(entity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);
            var bobombTransform = f.Unsafe.GetPointer<Transform2D>(entity);

            // Spawn coin
            EntityRef coinEntity = f.Create(f.SimulationConfig.LooseCoinPrototype);
            var coinTransform = f.Unsafe.GetPointer<Transform2D>(coinEntity);
            var coinPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(coinEntity);
            coinTransform->Position = bobombTransform->Position;
            coinPhysicsObject->Velocity.Y = f.RNG->Next(FP.FromString("4.5"), 5);

            // Fall off screen
            var killerTransform = f.Unsafe.GetPointer<Transform2D>(killerEntity);

            QuantumUtils.UnwrapWorldLocations(f, bobombTransform->Position, killerTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            enemy->FacingRight = ourPos.X > theirPos.X;
            physicsObject->DisableCollision = true;
            physicsObject->Velocity = new FPVector2(
                2 * (enemy->FacingRight ? 1 : -1),
                FP.FromString("2.5")
            );
            physicsObject->Gravity = FPVector2.Down * FP.FromString("14.75");

            byte combo;
            if (f.Unsafe.TryGetPointer(killerEntity, out MarioPlayer* mario)) {
                combo = mario->Combo++;
            } else if (f.Unsafe.TryGetPointer(killerEntity, out Koopa* koopa)) {
                combo = koopa->Combo++;
            } else {
                combo = 0;
            }
            f.Events.PlayComboSound(f, entity, combo);

            enemy->IsDead = true;

            // Holdable
            var holdable = f.Unsafe.GetPointer<Holdable>(entity);
            if (f.Exists(holdable->Holder)) {
                var marioHolder = f.Unsafe.GetPointer<MarioPlayer>(holdable->Holder);
                marioHolder->HeldEntity = default;
                holdable->PreviousHolder = default;
                holdable->Holder = default;
            }

            f.Events.EnemyKilled(f, entity, killerEntity, special);
        }
    }
}