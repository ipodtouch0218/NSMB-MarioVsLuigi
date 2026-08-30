using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct PiranhaPlant {
        public void Respawn(Frame f, EntityRef entity) {
            ChompFrames = 0;
            WaitingFrames = 216;
            PopupAnimationTime = 0;

            var enemy = f.Unsafe.GetPointer<Enemy>(entity);
            enemy->IsDead = false;
            enemy->IsActive = true;
        }

        public void Kill(Frame f, EntityRef piranhaPlantEntity, EntityRef killerEntity, EnemyKillReason reason) {
            var enemy = f.Unsafe.GetPointer<Enemy>(piranhaPlantEntity);

            var piranhaPlantTransform = f.Unsafe.GetPointer<Transform2D>(piranhaPlantEntity);
            var piranhaPlantCollider = f.Unsafe.GetPointer<PhysicsCollider2D>(piranhaPlantEntity);
            FPVector2 center = piranhaPlantTransform->Position + piranhaPlantCollider->Shape.Centroid;

            if (reason.ShouldSpawnCoin()) {
                // Spawn coin
                var gamemode = f.FindAsset(f.Global->Rules.Gamemode);
                gamemode.SpawnLooseCoin(f, center);
            }

            // Combo sound
            byte combo = ComboKeeper.IncrementOrDefault(f, killerEntity);
            f.Events.PlayComboSound(piranhaPlantEntity, combo);

            ChompFrames = 0;
            PopupAnimationTime = 0;
            enemy->IsDead = true;
            enemy->IsActive = false;
            enemy->SetDelayedRespawn();

            f.Unsafe.GetPointer<Interactable>(piranhaPlantEntity)->ColliderDisabled = true;

            f.Events.EnemyKilled(piranhaPlantEntity, killerEntity, reason, center);
        }
    }
}