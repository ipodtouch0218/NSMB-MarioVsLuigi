using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct Boo {
        public readonly void Respawn(Frame f, EntityRef entity) {
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);
            physicsObject->DisableCollision = true;
            physicsObject->Gravity = FPVector2.Zero;
        }

        public readonly void Kill(Frame f, EntityRef booEntity, EntityRef killerEntity, EnemyKillReason reason) {
            var enemy = f.Unsafe.GetPointer<Enemy>(booEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(booEntity);

            var booTransform = f.Unsafe.GetPointer<Transform2D>(booEntity);
            var booCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(booEntity);
            FPVector2 center = booTransform->Position + booCollider->Shape.Centroid;

            // Fall off screen
            var killerTransform = f.Unsafe.GetPointer<Transform2D>(killerEntity);

            QuantumUtils.UnwrapWorldLocations(f, booTransform->Position, killerTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
            enemy->ChangeFacingRight(f, booEntity, ourPos.X > theirPos.X);
            physicsObject->DisableCollision = true;
            physicsObject->Velocity = new FPVector2(
                2 * (enemy->FacingRight ? 1 : -1),
                Constants._2_50
            );
            physicsObject->Gravity = new FPVector2(0, -Constants._14_75);

            if (reason.ShouldSpawnCoin()) {
                // Spawn coin
                var gamemode = f.FindAsset(f.Global->Rules.Gamemode);
                gamemode.SpawnLooseCoin(f, center);
            }

            // Play combo sound
            byte combo = ComboKeeper.IncrementOrDefault(f, killerEntity);
            f.Events.PlayComboSound(booEntity, combo);

            enemy->IsDead = true;
            enemy->SetDelayedRespawn();

            f.Events.EnemyKilled(booEntity, killerEntity, reason, center);
        }
    }
}