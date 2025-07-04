using Photon.Deterministic;
using Quantum.Collections;

namespace Quantum {
    public unsafe class LiquidSystem : SystemSignalsOnly, ISignalOnTriggerEnter2D, ISignalOnTrigger2D, ISignalOnTriggerExit2D {

        public void OnTriggerEnter2D(Frame f, TriggerInfo2D info) {
            EntityRef physicsObjectEntity = info.Entity;
            EntityRef liquidEntity = info.Other;

            if (!f.Unsafe.TryGetPointer(liquidEntity, out Liquid* liquid)
                || !f.Unsafe.TryGetPointer(liquidEntity, out Transform2D* liquidTransform)
                || !f.Unsafe.TryGetPointer(physicsObjectEntity, out Transform2D* entityTransform)
                || !f.Unsafe.TryGetPointer(physicsObjectEntity, out PhysicsCollider2D* entityCollider)
                || !f.Unsafe.TryGetPointer(physicsObjectEntity, out PhysicsObject* entityPhysicsObject)) {
                return;
            }

            bool callSignals = true;
            if (f.Unsafe.TryGetPointer(physicsObjectEntity, out Interactable* interactable)
                && interactable->ColliderDisabled) {
                callSignals = false;
            } 

            FP surface = liquid->GetSurfaceHeight(liquidTransform);
            FP checkHeight = entityTransform->Position.Y + entityCollider->Shape.Centroid.Y - (entityPhysicsObject->Velocity.Y * f.DeltaTime);
            bool isEntityUnderwater = checkHeight <= surface;

            QHashSet<EntityRef> splashed = f.ResolveHashSet(liquid->SplashedEntities);
            if (!splashed.Contains(physicsObjectEntity)) {
                // Enter splash
                splashed.Add(physicsObjectEntity);

                var colliders = f.ResolveHashSet(entityPhysicsObject->LiquidContacts);
                colliders.Add(liquidEntity);

                if (callSignals) {
                    bool doSplash = !isEntityUnderwater;
                    f.Signals.OnTryLiquidSplash(physicsObjectEntity, liquidEntity, false, &doSplash);
                    if (doSplash) {
                        f.Events.LiquidSplashed(liquidEntity, physicsObjectEntity, FPMath.Abs(entityPhysicsObject->Velocity.Y), new FPVector2(entityTransform->Position.X, surface), false);
                    }
                }
            }
        }

        public void OnTrigger2D(Frame f, TriggerInfo2D info) {
            EntityRef physicsObjectEntity = info.Entity;
            EntityRef liquidEntity = info.Other;

            if (!f.Unsafe.TryGetPointer(liquidEntity, out Liquid* liquid)
                || !f.Unsafe.TryGetPointer(liquidEntity, out Transform2D* liquidTransform)
                || !f.Unsafe.TryGetPointer(physicsObjectEntity, out Transform2D* entityTransform)
                || !f.Unsafe.TryGetPointer(physicsObjectEntity, out PhysicsCollider2D* entityCollider)
                || !f.Unsafe.TryGetPointer(physicsObjectEntity, out PhysicsObject* entityPhysicsObject)) {
                return;
            }

            FP surface = liquid->GetSurfaceHeight(liquidTransform);
            FP checkHeight = entityTransform->Position.Y + entityCollider->Shape.Centroid.Y;
            bool isEntityUnderwater = checkHeight <= surface;

            QHashSet<EntityRef> underwater = f.ResolveHashSet(liquid->UnderwaterEntities);
            if (isEntityUnderwater && !underwater.Contains(physicsObjectEntity)) {
                // Enter state
                underwater.Add(physicsObjectEntity);
                f.Signals.OnEntityEnterExitLiquid(physicsObjectEntity, liquidEntity, true);
            } else if (!isEntityUnderwater && underwater.Contains(physicsObjectEntity)) {
                // Exit state
                underwater.Remove(physicsObjectEntity);
                f.Signals.OnEntityEnterExitLiquid(physicsObjectEntity, liquidEntity, false);
            }
        }

        public void OnTriggerExit2D(Frame f, ExitInfo2D info) {
            EntityRef physicsObjectEntity = info.Entity;
            EntityRef liquidEntity = info.Other;

            if (!f.Unsafe.TryGetPointer(liquidEntity, out Liquid* liquid)
                || !f.Unsafe.TryGetPointer(liquidEntity, out Transform2D* liquidTransform)
                || !f.Unsafe.TryGetPointer(physicsObjectEntity, out Transform2D* entityTransform)
                || !f.Unsafe.TryGetPointer(physicsObjectEntity, out PhysicsCollider2D* entityCollider)
                || !f.Unsafe.TryGetPointer(physicsObjectEntity, out PhysicsObject* entityPhysicsObject)) {
                return;
            }

            FP surface = liquid->GetSurfaceHeight(liquidTransform);
            FP checkHeight = entityTransform->Position.Y + entityCollider->Shape.Centroid.Y;
            bool isEntityUnderwater = checkHeight <= surface;


            bool callSignals = true;
            if (f.Unsafe.TryGetPointer(physicsObjectEntity, out Interactable* interactable)
                && interactable->ColliderDisabled) {
                callSignals = false;
            }

            QHashSet<EntityRef> splashed = f.ResolveHashSet(liquid->SplashedEntities);
            QHashSet<EntityRef> underwater = f.ResolveHashSet(liquid->UnderwaterEntities);

            if (splashed.Remove(physicsObjectEntity)) {
                // Exit splash
                // "checkHeight - surface < 1" prevents teleportation splashes

                var colliders = f.ResolveHashSet(entityPhysicsObject->LiquidContacts);
                colliders.Remove(liquidEntity);

                if (callSignals) {
                    bool doSplash = !isEntityUnderwater && (f.Number - entityTransform->PositionTeleportFrame) > 3;
                    f.Signals.OnTryLiquidSplash(physicsObjectEntity, liquidEntity, true, &doSplash);

                    if (doSplash) {
                        f.Events.LiquidSplashed(liquidEntity, physicsObjectEntity, FPMath.Abs(entityPhysicsObject->Velocity.Y), new FPVector2(entityTransform->Position.X, surface), true);
                    }
                }
            }

            if (underwater.Remove(physicsObjectEntity)) {
                // Exit state
                f.Signals.OnEntityEnterExitLiquid(physicsObjectEntity, liquidEntity, false);
            }
        }
    }
}