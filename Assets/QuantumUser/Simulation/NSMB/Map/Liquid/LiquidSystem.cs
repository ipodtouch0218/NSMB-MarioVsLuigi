using Photon.Deterministic;
using Quantum.Collections;

namespace Quantum {
    public unsafe class LiquidSystem : SystemSignalsOnly, ISignalOnComponentAdded<Liquid>, 
        ISignalOnTriggerEnter2D, ISignalOnTrigger2D, ISignalOnTriggerExit2D {

        public void OnTriggerEnter2D(Frame f, TriggerInfo2D info) {
            if (!f.Unsafe.TryGetPointer(info.Other, out Liquid* liquid)
                || !f.Unsafe.TryGetPointer(info.Other, out Transform2D* liquidTransform)
                || !f.Unsafe.TryGetPointer(info.Entity, out Transform2D* entityTransform)
                || !f.Unsafe.TryGetPointer(info.Entity, out PhysicsCollider2D* entityCollider)
                || !f.Unsafe.TryGetPointer(info.Entity, out PhysicsObject* entityPhysicsObject)) {
                return;
            }

            FP surface = liquid->GetSurfaceHeight(liquidTransform);
            FP checkHeight = entityTransform->Position.Y + entityCollider->Shape.Centroid.Y;
            bool isEntityUnderwater = liquid->LiquidType != LiquidType.Water || checkHeight <= surface;

            QHashSet<EntityRef> splashed = f.ResolveHashSet(liquid->SplashedEntities);
            if (!splashed.Contains(info.Entity)) {
                // Enter splash
                splashed.Add(info.Entity);

                if (!isEntityUnderwater) {
                    bool doSplash = true;
                    f.Signals.OnTryLiquidSplash(info.Entity, info.Other, false, &doSplash);
                    if (doSplash) {
                        f.Events.LiquidSplashed(info.Entity, FPMath.Abs(entityPhysicsObject->Velocity.Y), new FPVector2(entityTransform->Position.X, surface), false);
                    }
                }
            }
        }

        public void OnTrigger2D(Frame f, TriggerInfo2D info) {
            if (!f.Unsafe.TryGetPointer(info.Other, out Liquid* liquid)
                || !f.Unsafe.TryGetPointer(info.Other, out Transform2D* liquidTransform)
                || !f.Unsafe.TryGetPointer(info.Entity, out Transform2D* entityTransform)
                || !f.Unsafe.TryGetPointer(info.Entity, out PhysicsCollider2D* entityCollider)
                || !f.Unsafe.TryGetPointer(info.Entity, out PhysicsObject* entityPhysicsObject)) {
                return;
            }

            FP surface = liquid->GetSurfaceHeight(liquidTransform);
            FP checkHeight = entityTransform->Position.Y + entityCollider->Shape.Centroid.Y;
            bool isEntityUnderwater = liquid->LiquidType != LiquidType.Water || checkHeight <= surface;

            QHashSet<EntityRef> underwater = f.ResolveHashSet(liquid->UnderwaterEntities);
            if (isEntityUnderwater && !underwater.Contains(info.Entity)) {
                // Enter state
                underwater.Add(info.Entity);
                f.Signals.OnEntityEnterExitLiquid(info.Entity, info.Other, true);
            } else if (!isEntityUnderwater && underwater.Contains(info.Entity)) {
                // Exit state
                underwater.Remove(info.Entity);
                f.Signals.OnEntityEnterExitLiquid(info.Entity, info.Other, false);
            }
        }

        public void OnTriggerExit2D(Frame f, ExitInfo2D info) {
            if (!f.Unsafe.TryGetPointer(info.Other, out Liquid* liquid)
                || !f.Unsafe.TryGetPointer(info.Other, out Transform2D* liquidTransform)
                || !f.Unsafe.TryGetPointer(info.Entity, out Transform2D* entityTransform)
                || !f.Unsafe.TryGetPointer(info.Entity, out PhysicsCollider2D* entityCollider)
                || !f.Unsafe.TryGetPointer(info.Entity, out PhysicsObject* liquidPhysicsObject)) {
                return;
            }

            FP surface = liquid->GetSurfaceHeight(liquidTransform);
            FP checkHeight = entityTransform->Position.Y + entityCollider->Shape.Centroid.Y;
            bool isEntityUnderwater = liquid->LiquidType != LiquidType.Water || checkHeight <= surface;

            QHashSet<EntityRef> splashed = f.ResolveHashSet(liquid->SplashedEntities);
            QHashSet<EntityRef> underwater = f.ResolveHashSet(liquid->UnderwaterEntities);

            if (!isEntityUnderwater && splashed.Remove(info.Entity)) {
                // Exit splash
                bool doSplash = true;
                f.Signals.OnTryLiquidSplash(info.Entity, info.Other, true, &doSplash);
                if (doSplash) {
                    f.Events.LiquidSplashed(info.Entity, FPMath.Abs(liquidPhysicsObject->Velocity.Y), new FPVector2(entityTransform->Position.X, surface), true);
                }
            }

            if (underwater.Remove(info.Entity)) {
                // Exit state
                f.Signals.OnEntityEnterExitLiquid(info.Entity, info.Other, false);
            }
        }

        public void OnAdded(Frame f, EntityRef entity, Liquid* component) {
            var collider = f.Unsafe.GetPointer<PhysicsCollider2D>(entity);
            collider->Shape.Centroid = new(0, component->HeightTiles * FP._0_25 - FP._0_10);
            collider->Shape.Box.Extents = new(component->WidthTiles * FP._0_25, component->HeightTiles * FP._0_25);
        }
    }
}