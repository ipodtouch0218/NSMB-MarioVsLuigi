using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct Goomba {
        public void Kill(Frame f, EntityRef entity, EntityRef killerEntity, bool special) {
            var enemy = f.Unsafe.GetPointer<Enemy>(entity);
            var goomba = f.Unsafe.GetPointer<Goomba>(entity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);

            if (special) {
                var goombaTransform = f.Unsafe.GetPointer<Transform2D>(entity);

                // Spawn coin
                EntityRef coinEntity = f.Create(f.SimulationConfig.LooseCoinPrototype);
                var coinTransform = f.Unsafe.GetPointer<Transform2D>(coinEntity);
                var coinPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(coinEntity);
                coinTransform->Position = goombaTransform->Position;
                coinPhysicsObject->Velocity.Y = f.RNG->Next(Constants._4_50, 5);

                // Fall off screen
                if (f.Unsafe.TryGetPointer(killerEntity, out Transform2D* killerTransform)) {
                    QuantumUtils.UnwrapWorldLocations(f, goombaTransform->Position, killerTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
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
            } else {
                // Freeze and do squish animation
                physicsObject->IsFrozen = true;
                goomba->DeathAnimationFrames = 30;
            }

            enemy->IsDead = true;
            f.Events.EnemyKilled(f, entity, killerEntity, special);
        }
    }
}