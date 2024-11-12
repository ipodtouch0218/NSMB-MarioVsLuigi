using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct Bobomb {

        public void Respawn(Frame f, EntityRef entity) {
            CurrentDetonationFrames = 0;

            var holdable = f.Unsafe.GetPointer<Holdable>(entity);
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
                Constants._4_50 + speed,
                Constants._3_50
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
            coinPhysicsObject->Velocity.Y = f.RNG->Next(Constants._4_50, 5);

            // Fall off screen
            if (f.Unsafe.TryGetPointer(killerEntity, out Transform2D* killerTransform)) {
                QuantumUtils.UnwrapWorldLocations(f, bobombTransform->Position, killerTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
                enemy->ChangeFacingRight(f, entity, ourPos.X > theirPos.X);
            } else {
                enemy->ChangeFacingRight(f, entity, false);
            }

            physicsObject->DisableCollision = true;
            physicsObject->Velocity = new FPVector2(
                2 * (enemy->FacingRight ? 1 : -1),
                Constants._2_50
            );
            physicsObject->Gravity = new FPVector2(0, -Constants._14_75);

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
            if (f.Unsafe.TryGetPointer(holdable->Holder, out MarioPlayer* marioHolder)) {
                marioHolder->HeldEntity = default;
                holdable->PreviousHolder = default;
                holdable->Holder = default;
            }

            f.Events.EnemyKilled(f, entity, killerEntity, special);
        }
    }
}