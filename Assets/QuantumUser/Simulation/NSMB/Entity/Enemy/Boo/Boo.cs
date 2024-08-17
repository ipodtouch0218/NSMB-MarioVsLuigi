using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct Boo {
        public void Respawn(Frame f, EntityRef entity) {
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);
            physicsObject->DisableCollision = true;
            physicsObject->Gravity = FPVector2.Zero;
        }

        public void Kill(Frame f, EntityRef entity, EntityRef killerEntity, bool special) {
            var enemy = f.Unsafe.GetPointer<Enemy>(entity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);

            // Fall off screen
            var booTransform = f.Get<Transform2D>(entity);
            var killerTransform = f.Get<Transform2D>(killerEntity);

            QuantumUtils.UnwrapWorldLocations(f, booTransform.Position, killerTransform.Position, out FPVector2 ourPos, out FPVector2 theirPos);
            enemy->FacingRight = ourPos.X > theirPos.X;
            physicsObject->DisableCollision = true;
            physicsObject->Velocity = new FPVector2(
                2 * (enemy->FacingRight ? 1 : -1),
                FP.FromString("2.5")
            );
            physicsObject->Gravity = FPVector2.Down * FP.FromString("14.75");

            // Play combo sound
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