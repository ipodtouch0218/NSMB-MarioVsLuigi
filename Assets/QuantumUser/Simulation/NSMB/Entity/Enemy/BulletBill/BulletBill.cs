using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct BulletBill {
        public void Initialize(Frame f, EntityRef entity, EntityRef owner, bool right) {
            var enemy = f.Unsafe.GetPointer<Enemy>(entity);
            enemy->FacingRight = right;

            Owner = owner;
        }

        public void Kill(Frame f, EntityRef entity, EntityRef killerEntity, bool special) {
            var enemy = f.Unsafe.GetPointer<Enemy>(entity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);

            if (special) {
                // Spawn 
                enemy->IsActive = false;
            } else {
                // Fall off screen
                var transform = f.Get<Transform2D>(entity);
                var killerTransform = f.Get<Transform2D>(killerEntity);

                QuantumUtils.UnwrapWorldLocations(f, transform.Position, killerTransform.Position, out FPVector2 ourPos, out FPVector2 theirPos);
                enemy->FacingRight = ourPos.X > theirPos.X;
                physicsObject->DisableCollision = true;
                physicsObject->Velocity = new FPVector2(
                    2 * (enemy->FacingRight ? 1 : -1),
                    FP.FromString("2.5")
                );
                physicsObject->Gravity = FPVector2.Down * FP.FromString("14.75");
            }

            // Play sound
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
            f.Events.EnemyKilled(f, entity, killerEntity, special);
        }
    }

}