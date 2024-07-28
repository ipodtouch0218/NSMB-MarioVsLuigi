using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct Koopa {

        public void Respawn(Frame f, EntityRef entity) {
            var holdable = f.Unsafe.GetPointer<Holdable>(entity);

            IsInShell = false;
            IsKicked = false;
            Combo = 0;
            CurrentSpeed = Speed;
            holdable->Holder = default;
            holdable->PreviousHolder = default;
        }

        public void EnterShell(Frame f, EntityRef entity, EntityRef initiator, bool flipped) {
            var holdable = f.Unsafe.GetPointer<Holdable>(entity);
            holdable->PreviousHolder = initiator;

            if (!IsInShell || IsKicked) {
                f.Events.KoopaEnteredShell(f, entity);
            }

            CurrentSpeed = 0;
            Combo = 0;
            IsInShell = true;
            IsKicked = false;
            IsFlipped |= flipped;

            WakeupFrames = 15 * 60;
        }

        public void Kick(Frame f, EntityRef entity, EntityRef initiator, FP speed) {
            var holdable = f.Unsafe.GetPointer<Holdable>(entity);
            holdable->PreviousHolder = initiator;

            IgnoreOwnerFrames = 15;
            IsKicked = true;
            IsInShell = true;
            Combo = 0;
            CurrentSpeed = KickSpeed + speed;

            f.Events.PlayComboSound(f, entity, 0);
        }

        public void Kill(Frame f, EntityRef entity, EntityRef killerEntity, bool special) {
            var enemy = f.Unsafe.GetPointer<Enemy>(entity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);
            var holdable = f.Unsafe.GetPointer<Holdable>(entity);

            if (f.Exists(holdable->Holder)) {
                f.Unsafe.GetPointer<MarioPlayer>(holdable->Holder)->HeldEntity = default;
                holdable->PreviousHolder = holdable->Holder;
                holdable->Holder = default;
                IgnoreOwnerFrames = 15;
            }

            // Fall off screen
            var koopaTransform = f.Get<Transform2D>(entity);
            var killerTransform = f.Get<Transform2D>(killerEntity);

            QuantumUtils.UnwrapWorldLocations(f, koopaTransform.Position, killerTransform.Position, out FPVector2 ourPos, out FPVector2 theirPos);
            enemy->FacingRight = ourPos.X > theirPos.X;
            physicsObject->DisableCollision = true;
            physicsObject->Velocity = new FPVector2(
                2 * (enemy->FacingRight ? 1 : -1),
                FP.FromString("2.5")
            );
            physicsObject->Gravity = FPVector2.Down * FP.FromString("14.75");

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
            IsInShell = false;
            IsKicked = false;
            IsFlipped = false;
            f.Events.EnemyKilled(f, entity, killerEntity, false);
        }
    }
}