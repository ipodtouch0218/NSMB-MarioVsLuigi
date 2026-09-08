using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct BulletBill {
        public void Initialize(Frame f, EntityRef entity, EntityRef owner, bool right) {
            var enemy = f.Unsafe.GetPointer<Enemy>(entity);
            enemy->FacingRight = right;
            enemy->IsActive = true;
            enemy->IsDead = false;

            Owner = owner;
        }

        public readonly void Kill(Frame f, EntityRef bulletBillEntity, EntityRef killerEntity, EnemyKillReason reason) {
            var enemy = f.Unsafe.GetPointer<Enemy>(bulletBillEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(bulletBillEntity);

            bool playSound;
            if (reason == EnemyKillReason.Normal) {
                // Stomped, fall off screen
                physicsObject->DisableCollision = true;
                physicsObject->Velocity = new FPVector2(
                    2 * (enemy->FacingRight ? 1 : -1),
                    0
                );
                physicsObject->Gravity = new FPVector2(0, -Constants._14_75);
                playSound = f.Has<Holdable>(killerEntity);
            } else {
                // Special kill, disappear.
                enemy->IsActive = false;
                physicsObject->IsFrozen = true;
                playSound = true;
            }

            if (playSound) {
                byte combo = ComboKeeper.IncrementOrDefault(f, killerEntity);
                f.Events.PlayComboSound(bulletBillEntity, combo);
            }

            enemy->IsDead = true;
            f.Unsafe.GetPointer<Interactable>(bulletBillEntity)->ColliderDisabled = true;

            var collider = f.Unsafe.GetPointer<PhysicsCollider2D>(bulletBillEntity);
            FPVector2 center = f.Unsafe.GetPointer<Transform2D>(bulletBillEntity)->Position + collider->Shape.Centroid;
            f.Events.EnemyKilled(bulletBillEntity, killerEntity, reason, center);
        }
    }
}
