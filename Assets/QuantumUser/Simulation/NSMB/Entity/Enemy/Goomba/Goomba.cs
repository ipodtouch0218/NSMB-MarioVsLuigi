using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct Goomba {
        public void Kill(Frame f, EntityRef entity, EntityRef killerEntity, bool special) {
            var enemy = f.Unsafe.GetPointer<Enemy>(entity);
            var goomba = f.Unsafe.GetPointer<Goomba>(entity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);

            if (special) {
                // Spawn coin
                EntityRef coinEntity = f.Create(f.SimulationConfig.LooseCoinPrototype);
                var coinTransform = f.Unsafe.GetPointer<Transform2D>(coinEntity);
                var coinPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(coinEntity);
                coinTransform->Position = f.Get<Transform2D>(entity).Position;
                coinPhysicsObject->Velocity.Y = f.RNG->Next(FP.FromString("4.5"), 5);

                // Fall off screen
                var goombaTransform = f.Get<Transform2D>(entity);
                var killerTransform = f.Get<Transform2D>(killerEntity);

                QuantumUtils.UnwrapWorldLocations(f, goombaTransform.Position, killerTransform.Position, out FPVector2 ourPos, out FPVector2 theirPos);
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