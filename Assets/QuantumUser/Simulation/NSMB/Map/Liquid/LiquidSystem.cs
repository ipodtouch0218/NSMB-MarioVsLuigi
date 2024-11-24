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
            FP checkHeight = entityTransform->Position.Y + entityCollider->Shape.Centroid.Y - entityPhysicsObject->Velocity.Y;
            bool isEntityUnderwater = checkHeight <= surface;

            QHashSet<EntityRef> splashed = f.ResolveHashSet(liquid->SplashedEntities);
            if (!splashed.Contains(info.Entity)) {
                // Enter splash
                splashed.Add(info.Entity);

                bool doSplash = !isEntityUnderwater;
                f.Signals.OnTryLiquidSplash(info.Entity, info.Other, false, &doSplash);
                if (doSplash) {
                    f.Events.LiquidSplashed(f, info.Other, info.Entity, FPMath.Abs(entityPhysicsObject->Velocity.Y), new FPVector2(entityTransform->Position.X, surface), false);
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
            bool isEntityUnderwater = checkHeight <= surface;

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
            bool isEntityUnderwater = checkHeight <= surface;

            QHashSet<EntityRef> splashed = f.ResolveHashSet(liquid->SplashedEntities);
            QHashSet<EntityRef> underwater = f.ResolveHashSet(liquid->UnderwaterEntities);

            if (splashed.Remove(info.Entity)) {
                // Exit splash
                // "checkHeight - surface < 1" prevents teleportation splashes
                bool doSplash = !isEntityUnderwater && checkHeight - surface < 1;
                f.Signals.OnTryLiquidSplash(info.Entity, info.Other, true, &doSplash);
                
                if (doSplash) {
                    f.Events.LiquidSplashed(f, info.Other, info.Entity, FPMath.Abs(liquidPhysicsObject->Velocity.Y), new FPVector2(entityTransform->Position.X, surface), true);
                }
            }

            if (underwater.Remove(info.Entity)) {
                // Exit state
                f.Signals.OnEntityEnterExitLiquid(info.Entity, info.Other, false);
            }
        }

        public void OnAdded(Frame f, EntityRef entity, Liquid* component) {
            var collider = f.Unsafe.GetPointer<PhysicsCollider2D>(entity);

            Shape2D shape = Shape2D.CreatePersistentCompound();
            FP totalWidth = component->WidthTiles * FP._0_50;
            FP centroidY = component->HeightTiles * FP._0_25 - FP._0_10;

            int sections = FPMath.CeilToInt(totalWidth / 4);
            FPVector2 extents = new((totalWidth / sections) / 2, component->HeightTiles * FP._0_25);
            for (int i = 0; i < sections; i++) {
                FP centroidX = ((2 * i + 1) * extents.X) - (totalWidth / 2);
                Shape2D newBox = Shape2D.CreateBox(extents, new FPVector2(centroidX, centroidY));
                shape.Compound.AddShape(f, ref newBox);
            }

            collider->Shape = shape;
        }
    }
}