using Photon.Deterministic;
using UnityEngine;

namespace Quantum {

    public unsafe class BlockBumpSystem : SystemMainThreadFilter<BlockBumpSystem.Filter> {

        private static readonly FPVector2 BumpOffset = new FPVector2(0, -FP._0_25);

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public BlockBump* BlockBump;
            public PhysicsCollider2D* Collider;
        }

        public override void Update(Frame f, ref Filter filter) {
            var blockBump = filter.BlockBump;
            var collider = filter.Collider;
            var transform = filter.Transform;

            FP bumpSize = Constants._0_35;
            FPVector2 bumpScale = new(FP._0_25, FP._0_25);
            FP bumpDuration = FP._0_25;
            FPVector2 bumpOffset = BumpOffset;

            if (f.FindAsset(blockBump->StartTile) is BreakableBrickTile bbt) {
                bumpScale = bbt.BumpSize / 2;
                bumpOffset += bbt.BumpOffset;
            }

            bool kill = QuantumUtils.Decrement(ref blockBump->Lifetime);
            FP sizeAmount = FPMath.Sin(blockBump->Lifetime * f.DeltaTime / bumpDuration * FP.Pi);
            FPVector2 newSize = bumpScale + (bumpScale * sizeAmount) / 3;

            collider->Shape.Box.Extents = newSize;
            transform->Position =
                blockBump->Origin
                + bumpOffset
                + new FPVector2(0, blockBump->IsDownwards ? (bumpScale.Y * 2 - newSize.Y) : newSize.Y);

            if (!blockBump->HasBumped) {
                Bump(f, transform->Position, blockBump->Owner, blockBump->AllowSelfDamage, bumpScale.X, -bumpOffset.Y / 2);
                blockBump->HasBumped = true;
            }

            if (kill) {
                Kill(f, ref filter);
            }
        }

        public void Kill(Frame f, ref Filter filter) {
            var blockBump = filter.BlockBump;
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);

            stage.SetTileRelative(f, blockBump->TileX, blockBump->TileY, blockBump->ResultTile);

            if (blockBump->Powerup.IsValid) {
                EntityRef newPowerup = f.Create(blockBump->Powerup);
                if (f.Unsafe.TryGetPointer(newPowerup, out Powerup* powerup)) {
                    // Launch if downwards bump and theres a (solid) block below us
                    BreakableBrickTile tile = (BreakableBrickTile) f.FindAsset(blockBump->StartTile);
                    StageTileInstance belowTileInstance = stage.GetTileRelative(f, blockBump->TileX, blockBump->TileY - FPMath.RoundToInt(tile.BumpSize.Y * 2));
                    bool launch = blockBump->IsDownwards && belowTileInstance.HasWorldPolygons(f);

                    FP powerupHeight = f.Unsafe.GetPointer<PhysicsCollider2D>(newPowerup)->Shape.Box.Extents.Y;
                    FPVector2 origin = filter.Transform->Position;

                    var powerupScriptable = f.FindAsset(powerup->Scriptable);
                    if (powerupScriptable.State == PowerupState.MegaMushroom) {
                        origin.Y += (tile.BumpSize.Y / 2) - FP._0_50;

                        powerup->Initialize(f, newPowerup, 90, 
                            origin + FPVector2.Up * FP._0_50, 
                            origin + FPVector2.Up * FP._0_50, 
                            false);
                    } else {
                        if (blockBump->IsDownwards) {
                            origin.Y -= (tile.BumpSize.Y / 2);
                        } else {
                            origin.Y += (tile.BumpSize.Y / 2) - FP._0_50;
                        }

                        powerup->Initialize(f, newPowerup, (byte) (launch ? 20 : 60),
                            origin, 
                            origin + (blockBump->IsDownwards ? new FPVector2(0, -FP._0_50) : new FPVector2(0, FP._0_50)), 
                            launch);
                    }
                }
            }

            f.Destroy(filter.Entity);
        }

        public static void Bump(Frame f, FPVector2 position, EntityRef bumpee, bool allowSelfDamage, FP? width = null, FP? height = null) {
            // TODO change extents to be customizable
            FPVector2 extents = new(width ?? FP._0_25, FP._0_10);
            Transform2D transform = new() {
                Position = position + new FPVector2(0, (extents.Y * 2) + (height ?? FP._0_25))
            };

            Draw.Rectangle(transform.Position, extents * 2, 0);

            var hits = f.Physics2D.OverlapShape(transform, Shape2D.CreateBox(extents));
            for (int i = 0; i < hits.Count; i++) {
                var hit = hits[i];
                if (bumpee == hit.Entity && !allowSelfDamage) {
                    continue;
                }

                f.Signals.OnEntityBumped(hit.Entity, position, bumpee);
            }
        }
    }
}