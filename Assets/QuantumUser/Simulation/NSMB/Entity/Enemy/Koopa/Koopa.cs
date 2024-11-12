using Photon.Deterministic;

namespace Quantum {
    public unsafe partial struct Koopa {

        public void Respawn(Frame f, EntityRef entity) {
            var holdable = f.Unsafe.GetPointer<Holdable>(entity);

            IsInShell = false;
            IsKicked = false;
            IsFlipped = false;
            Combo = 0;
            CurrentSpeed = Speed;
            holdable->Holder = default;
            holdable->PreviousHolder = default;
        }

        public void EnterShell(Frame f, EntityRef entity, EntityRef initiator, bool flipped, bool groundpounded) {
            var holdable = f.Unsafe.GetPointer<Holdable>(entity);
            holdable->PreviousHolder = initiator;
            /*
            if (!IsInShell || IsKicked || groundpounded) {
                f.Events.KoopaEnteredShell(f, entity, groundpounded);
            }
            */
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
            holdable->IgnoreOwnerFrames = 15;

            IsKicked = true;
            IsInShell = true;
            Combo = 0;
            CurrentSpeed = KickSpeed + speed;

            f.Events.PlayComboSound(f, entity, 0);
            f.Events.KoopaKicked(f, entity, false);
        }

        public void Kill(Frame f, EntityRef entity, EntityRef killerEntity, bool special) {
            var enemy = f.Unsafe.GetPointer<Enemy>(entity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);
            var holdable = f.Unsafe.GetPointer<Holdable>(entity);

            if (f.Exists(holdable->Holder)) {
                f.Unsafe.GetPointer<MarioPlayer>(holdable->Holder)->HeldEntity = default;
                holdable->PreviousHolder = holdable->Holder;
                holdable->Holder = default;
                holdable->IgnoreOwnerFrames = 15;
            }

            var koopaTransform = f.Unsafe.GetPointer<Transform2D>(entity);

            // Spawn coin
            EntityRef coinEntity = f.Create(f.SimulationConfig.LooseCoinPrototype);
            var coinTransform = f.Unsafe.GetPointer<Transform2D>(coinEntity);
            var coinPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(coinEntity);
            coinTransform->Position = koopaTransform->Position;
            coinPhysicsObject->Velocity.Y = f.RNG->Next(Constants._4_50, 5);

            // Fall off screen
            if (f.Unsafe.TryGetPointer(killerEntity, out Transform2D* killerTransform)) {
                QuantumUtils.UnwrapWorldLocations(f, koopaTransform->Position, killerTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
                enemy->ChangeFacingRight(f, entity, ourPos.X > theirPos.X);
            } else {
                enemy->ChangeFacingRight(f, entity, false);
            }

            physicsObject->DisableCollision = true;
            physicsObject->Velocity = new FPVector2(
                2 * (enemy->FacingRight ? 1 : -1),
                Constants._2_50
            );
            physicsObject->Gravity = new FPVector2(0, -Constants._14_75);

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