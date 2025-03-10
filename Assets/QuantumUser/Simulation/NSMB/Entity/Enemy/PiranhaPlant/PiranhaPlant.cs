using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct PiranhaPlant {
        public void Respawn(Frame f, EntityRef entity) {
            ChompFrames = 0;
            WaitingFrames = 216;
            PopupAnimationTime = 0;
        }

        public void Kill(Frame f, EntityRef piranhaPlantEntity, EntityRef killerEntity, KillReason reason) {
            var enemy = f.Unsafe.GetPointer<Enemy>(piranhaPlantEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(piranhaPlantEntity);

            FPVector2 position = f.Unsafe.GetPointer<Transform2D>(piranhaPlantEntity)->Position;

            if (reason.ShouldSpawnCoin()) {
                // Spawn coin
                EntityRef coinEntity = f.Create(f.SimulationConfig.LooseCoinPrototype);
                var coinTransform = f.Unsafe.GetPointer<Transform2D>(coinEntity);
                var coinPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(coinEntity);
                coinTransform->Position = position;
                coinPhysicsObject->Velocity.Y = f.RNG->Next(Constants._4_50, 5);
            }

            // Combo sound
            byte combo;
            if (f.Unsafe.TryGetPointer(killerEntity, out ComboKeeper* comboKeeper)) {
                combo = comboKeeper->Combo++;
            } else {
                combo = 0;
            }
            f.Events.PlayComboSound(piranhaPlantEntity, combo);

            ChompFrames = 0;
            PopupAnimationTime = 0;
            enemy->IsDead = true;
            enemy->IsActive = false;

            f.Unsafe.GetPointer<Interactable>(piranhaPlantEntity)->ColliderDisabled = true;

            var collider = f.Unsafe.GetPointer<PhysicsCollider2D>(piranhaPlantEntity);
            FPVector2 center = position + collider->Shape.Centroid;
            f.Events.EnemyKilled(piranhaPlantEntity, killerEntity, reason, center);
        }
    }
}