using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct Boo {
        public void Respawn(Frame f, EntityRef entity) {
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);
            physicsObject->DisableCollision = true;
            physicsObject->Gravity = FPVector2.Zero;
        }

        public void Kill(Frame f, EntityRef booEntity, EntityRef killerEntity, KillReason reason) {
            var enemy = f.Unsafe.GetPointer<Enemy>(booEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(booEntity);

            // Fall off screen
            var booTransform = f.Unsafe.GetPointer<Transform2D>(booEntity);
            var killerTransform = f.Unsafe.GetPointer<Transform2D>(killerEntity);

            QuantumUtils.UnwrapWorldLocations(f, booTransform->Position, killerTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            enemy->ChangeFacingRight(f, booEntity, ourPos.X > theirPos.X);
            physicsObject->DisableCollision = true;
            physicsObject->Velocity = new FPVector2(
                2 * (enemy->FacingRight ? 1 : -1),
                Constants._2_50
            );
            physicsObject->Gravity = new FPVector2(0, -Constants._14_75);

            // Play combo sound
            byte combo;
            if (f.Unsafe.TryGetPointer(killerEntity, out ComboKeeper* comboKeeper)) {
                combo = comboKeeper->Combo++;
            } else {
                combo = 0;
            }
            f.Events.PlayComboSound(booEntity, combo);

            enemy->IsDead = true;

            var collider = f.Unsafe.GetPointer<PhysicsCollider2D>(booEntity);
            FPVector2 center = booTransform->Position + collider->Shape.Centroid;
            f.Events.EnemyKilled(booEntity, killerEntity, reason, center);
        }
    }
}