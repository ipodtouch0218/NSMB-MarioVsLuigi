using Photon.Deterministic;
using Quantum.Collections;

namespace Quantum {
    public unsafe class LiquidSystem : SystemSignalsOnly, ISignalOnComponentAdded<Liquid>, ISignalOnTrigger2D, ISignalOnTriggerExit2D {

        public void OnTrigger2D(Frame f, TriggerInfo2D info) {
            if (!f.Unsafe.TryGetPointer(info.Other, out Liquid* liquid) ||
                !f.TryGet(info.Other, out Transform2D ourTransform) ||
                !f.TryGet(info.Entity, out Transform2D entityTransform) ||
                !f.TryGet(info.Entity, out PhysicsCollider2D collider) ||
                !f.TryGet(info.Entity, out PhysicsObject physicsObject)) {
                return;
            }

            FP surface = liquid->GetSurfaceHeight(ourTransform);
            FP checkHeight = entityTransform.Position.Y + collider.Shape.Centroid.Y;
            bool underwater = checkHeight <= surface;

            QList<EntityRef> splashed = f.ResolveList(liquid->SplashedEntities);
            if (splashed.Contains(info.Entity)) {
                // Already splashed, check for potential exit...
                if (!underwater) {
                    // Exit splash
                    splashed.RemoveUnordered(info.Entity);

                    bool doSplash = true;
                    f.Signals.OnTryLiquidSplash(info.Entity, info.Other, &doSplash);
                    if (doSplash) {
                        f.Events.LiquidSplashed(info.Entity, FPMath.Abs(physicsObject.Velocity.Y), new FPVector2(entityTransform.Position.X, surface), true);
                    }

                    // Mario specific effects...
                    if (liquid->LiquidType == LiquidType.Water && f.Unsafe.TryGetPointer(info.Entity, out MarioPlayer* mario)) {
                        if (QuantumUtils.Decrement(ref mario->WaterColliderCount)) {
                            // Jump
                            mario->SwimExitForceJump = true;
                        }
                    }
                }
            } else {
                // Not splashed yet, check for potential entrance
                if (underwater) {
                    // Enter splash
                    splashed.Add(info.Entity);

                    bool doSplash = true;
                    f.Signals.OnTryLiquidSplash(info.Entity, info.Other, &doSplash);
                    if (doSplash) {
                        f.Events.LiquidSplashed(info.Entity, FPMath.Abs(physicsObject.Velocity.Y), new FPVector2(entityTransform.Position.X, surface), false);
                    }

                    // Mario specific effects...
                    if (f.Unsafe.TryGetPointer(info.Entity, out MarioPlayer* mario)) {
                        switch (liquid->LiquidType) {
                        case LiquidType.Water:
                            mario->WaterColliderCount++;
                            break;
                        case LiquidType.Lava:
                            // Kill, fire death
                            mario->Death(f, info.Entity, true);
                            break;
                        case LiquidType.Poison:
                            // Kill, normal death
                            mario->Death(f, info.Entity, false);
                            break;
                        }
                    }
                }
            }
        }

        public void OnTriggerExit2D(Frame f, ExitInfo2D info) {
            if (!f.Unsafe.TryGetPointer(info.Other, out Liquid* liquid) ||
                !f.TryGet(info.Other, out Transform2D ourTransform) ||
                !f.TryGet(info.Entity, out Transform2D entityTransform) ||
                !f.TryGet(info.Entity, out PhysicsCollider2D collider) ||
                !f.TryGet(info.Entity, out PhysicsObject physicsObject)) {
                return;
            }

            FP surface = liquid->GetSurfaceHeight(ourTransform);
            FP checkHeight = entityTransform.Position.Y + collider.Shape.Centroid.Y;
            bool underwater = checkHeight <= surface;

            QList<EntityRef> splashed = f.ResolveList(liquid->SplashedEntities);
            if (splashed.Contains(info.Entity)) {
                if (!underwater) {
                    // Exit splash
                    splashed.RemoveUnordered(info.Entity);

                    bool doSplash = true;
                    f.Signals.OnTryLiquidSplash(info.Entity, info.Other, &doSplash);
                    if (doSplash) {
                        f.Events.LiquidSplashed(info.Entity, FPMath.Abs(physicsObject.Velocity.Y), new FPVector2(entityTransform.Position.X, surface), true);
                    }
                }

                // Mario specific effects...
                if (liquid->LiquidType == LiquidType.Water && f.Unsafe.TryGetPointer(info.Entity, out MarioPlayer* mario)) {
                    if (QuantumUtils.Decrement(ref mario->WaterColliderCount) && !underwater) {
                        // Jump
                        mario->SwimExitForceJump = true;
                    }
                }
            }
        }

        public void OnAdded(Frame f, EntityRef entity, Liquid* component) {
            var collider = f.Unsafe.GetPointer<PhysicsCollider2D>(entity);
            collider->Shape.Centroid = new(0, component->HeightTiles * FP._0_25 - FP._0_10);
            collider->Shape.Box.Extents = new((FP) component->WidthTiles / 4, component->HeightTiles / 4);
            f.AllocateList(out component->SplashedEntities);
        }
    }
}