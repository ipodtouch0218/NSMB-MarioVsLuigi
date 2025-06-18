using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct Goomba {

        public void Respawn(Frame f, EntityRef entity) {
            f.Unsafe.GetPointer<Interactable>(entity)->ColliderDisabled = false;
        }

        public void Kill(Frame f, EntityRef goombaEntity, EntityRef killerEntity, KillReason reason) {
            var enemy = f.Unsafe.GetPointer<Enemy>(goombaEntity);
            var goomba = f.Unsafe.GetPointer<Goomba>(goombaEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(goombaEntity);

            var goombaTransform = f.Unsafe.GetPointer<Transform2D>(goombaEntity);
            var goombaCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(goombaEntity);
            FPVector2 center = goombaTransform->Position + goombaCollider->Shape.Centroid;

            if (reason.ShouldSpawnCoin()) {
                // Spawn coin
                var gamemode = f.FindAsset(f.Global->Rules.Gamemode);
                gamemode.SpawnLooseCoin(f, center);
            }

            if (reason != KillReason.Normal) {
                // Fall off screen
                if (f.Unsafe.TryGetPointer(killerEntity, out Transform2D* killerTransform)) {
                    QuantumUtils.UnwrapWorldLocations(f, goombaTransform->Position, killerTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
                    enemy->ChangeFacingRight(f, goombaEntity, ourPos.X > theirPos.X);
                } else {
                    enemy->ChangeFacingRight(f, goombaEntity, false);
                }

                physicsObject->DisableCollision = true;
                physicsObject->Velocity = new FPVector2(
                    2 * (enemy->FacingRight ? 1 : -1),
                    Constants._2_50
                );
                physicsObject->Gravity = new FPVector2(0, -Constants._14_75);

                byte combo;
                if (f.Unsafe.TryGetPointer(killerEntity, out ComboKeeper* comboKeeper)) {
                    combo = comboKeeper->Combo++;
                } else {
                    combo = 0;
                }
                f.Events.PlayComboSound(goombaEntity, combo);
            } else {
                // Freeze and do squish animation
                physicsObject->IsFrozen = true;
                goomba->DeathAnimationFrames = 30;
            }

            enemy->IsDead = true;
            f.Unsafe.GetPointer<Interactable>(goombaEntity)->ColliderDisabled = true;

            var collider = f.Unsafe.GetPointer<PhysicsCollider2D>(goombaEntity);
            f.Events.EnemyKilled(goombaEntity, killerEntity, reason, center);
        }
    }
}