using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct PiranhaPlant {
        public void Respawn(Frame f, EntityRef entity) {
            ChompFrames = 0;
            WaitingFrames = 216;
            PopupAnimationTime = 0;
        }

        public void Kill(Frame f, EntityRef entity, EntityRef killerEntity, bool special) {
            var enemy = f.Unsafe.GetPointer<Enemy>(entity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);

            // Spawn coin
            EntityRef coinEntity = f.Create(f.SimulationConfig.LooseCoinPrototype);
            var coinTransform = f.Unsafe.GetPointer<Transform2D>(coinEntity);
            var coinPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(coinEntity);
            coinTransform->Position = f.Unsafe.GetPointer<Transform2D>(entity)->Position;
            coinPhysicsObject->Velocity.Y = f.RNG->Next(Constants._4_50, 5);

            // Combo sound
            byte combo;
            if (f.Unsafe.TryGetPointer(killerEntity, out MarioPlayer* mario)) {
                combo = mario->Combo++;
            } else if (f.Unsafe.TryGetPointer(killerEntity, out Koopa* koopa)) {
                combo = koopa->Combo++;
            } else {
                combo = 0;
            }
            f.Events.PlayComboSound(f, entity, combo);

            ChompFrames = 0;
            PopupAnimationTime = 0;
            enemy->IsDead = true;
            enemy->IsActive = false;

            f.Events.EnemyKilled(f, entity, killerEntity, special);
        }
    }
}