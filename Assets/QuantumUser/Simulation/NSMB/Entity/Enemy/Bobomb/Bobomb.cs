using Photon.Deterministic;
using System.Runtime.InteropServices;

namespace Quantum {
    public unsafe partial struct Bobomb {

        public void Respawn(Frame f, EntityRef entity) {
            CurrentDetonationFrames = 0;

            var holdable = f.Unsafe.GetPointer<Holdable>(entity);
            holdable->Holder = default;
            holdable->PreviousHolder = default;
            holdable->IgnoreOwnerFrames = 0;

            f.Unsafe.GetPointer<Interactable>(entity)->ColliderDisabled = false;
        }

        public void Kick(Frame f, EntityRef entity, EntityRef initiator, FP speed) {
            var enemy = f.Unsafe.GetPointer<Enemy>(entity);
            var initiatorTransform = f.Unsafe.GetPointer<Transform2D>(initiator);
            var bobombTransform = f.Unsafe.GetPointer<Transform2D>(entity);
            
            enemy->FacingRight = QuantumUtils.WrappedDirectionSign(f, bobombTransform->Position, initiatorTransform->Position) > 0;

            var holdable = f.Unsafe.GetPointer<Holdable>(entity);
            holdable->PreviousHolder = initiator;
            holdable->IgnoreOwnerFrames = 15;

            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(entity);
            physicsObject->Velocity = new(
                (Constants._4_50 + speed) * (enemy->FacingRight ? 1 : -1),
                Constants._3_50
            );

            f.Events.PlayComboSound(entity, 0);
        }

        public void Kill(Frame f, EntityRef bobombEntity, EntityRef killerEntity, KillReason reason) {
            var enemy = f.Unsafe.GetPointer<Enemy>(bobombEntity);
            var physicsObject = f.Unsafe.GetPointer<PhysicsObject>(bobombEntity);
            var bobombTransform = f.Unsafe.GetPointer<Transform2D>(bobombEntity);

            FPVector2 position = bobombTransform->Position;

            if (reason.ShouldSpawnCoin()) {
                // Spawn coin
                var gamemode = f.FindAsset(f.Global->Rules.Gamemode);
                gamemode.SpawnLooseCoin(f, position);
            }

            // Fall off screen
            if (f.Unsafe.TryGetPointer(killerEntity, out Transform2D* killerTransform)) {
                QuantumUtils.UnwrapWorldLocations(f, position, killerTransform->Position, out FPVector2 ourPos, out FPVector2 theirPos);
                enemy->ChangeFacingRight(f, bobombEntity, ourPos.X > theirPos.X);
            } else {
                enemy->ChangeFacingRight(f, bobombEntity, false);
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
            f.Events.PlayComboSound(bobombEntity, combo);

            enemy->IsDead = true;

            // Holdable
            var holdable = f.Unsafe.GetPointer<Holdable>(bobombEntity);
            if (f.Unsafe.TryGetPointer(holdable->Holder, out MarioPlayer* marioHolder)) {
                marioHolder->HeldEntity = default;
                holdable->PreviousHolder = default;
                holdable->Holder = default;
            }

            f.Unsafe.GetPointer<Interactable>(bobombEntity)->ColliderDisabled = true;

            var collider = f.Unsafe.GetPointer<PhysicsCollider2D>(bobombEntity);
            FPVector2 center = position + collider->Shape.Centroid;
            f.Events.EnemyKilled(bobombEntity, killerEntity, reason, center);
        }
    }
}