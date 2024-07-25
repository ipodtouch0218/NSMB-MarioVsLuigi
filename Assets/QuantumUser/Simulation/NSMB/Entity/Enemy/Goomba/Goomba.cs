using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct Goomba {

        public void Reset(Frame f, EntityRef entity) {
            var transform = f.Unsafe.GetPointer<Transform2D>(entity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);

            IsActive = true;
            IsDead = false;
            FacingRight = false;
            transform->Position = Spawnpoint;
            physicsObject->IsFrozen = false;
            physicsObject->Velocity = FPVector2.Zero;
            physicsObject->DisableCollision = false;
        }

        public void Kill(Frame f, EntityRef entity, EntityRef killerEntity, bool special) {
            var goomba = f.Unsafe.GetPointer<Goomba>(entity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);

            if (special) {
                // Fall off screen
                var goombaTransform = f.Get<Transform2D>(entity);
                var killerTransform = f.Get<Transform2D>(killerEntity);

                QuantumUtils.UnwrapWorldLocations(f, goombaTransform.Position, killerTransform.Position, out FPVector2 ourPos, out FPVector2 theirPos);
                FacingRight = ourPos.X > theirPos.X;
                physicsObject->DisableCollision = true;
                physicsObject->Velocity = new FPVector2(
                    2 * (FacingRight ? 1 : -1),
                    FP.FromString("2.5")
                );
                physicsObject->Gravity = FPVector2.Down * FP.FromString("14.75");

                byte combo;
                if (f.Unsafe.TryGetPointer(killerEntity, out MarioPlayer* mario)) {
                    combo = mario->Combo++;
                } else {
                    combo = 0;
                }
                f.Events.PlayComboSound(f, entity, combo);
            } else {
                // Freeze and do squish animation
                physicsObject->IsFrozen = true;
                goomba->DeathAnimationFrames = 30;
            }

            IsDead = true;
            f.Events.EnemyKilled(f, entity, killerEntity, special);
        }
    }
}