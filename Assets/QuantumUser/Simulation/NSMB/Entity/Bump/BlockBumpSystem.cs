using Photon.Deterministic;

namespace Quantum {
    public unsafe class BlockBumpSystem : SystemMainThreadEntityFilter<BlockBump, BlockBumpSystem.Filter> {

        private static readonly FPVector2 BumpOffset = new FPVector2(0, -FP._0_25);

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public BlockBump* BlockBump;
            public PhysicsCollider2D* Collider;
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var blockBump = filter.BlockBump;
            var collider = filter.Collider;
            var transform = filter.Transform;

            FPVector2 bumpScale = new(FP._0_25, FP._0_25);
            FP bumpDuration = FP._0_25;
            FPVector2 bumpOffset = BumpOffset;

            if (f.FindAsset(blockBump->StartTile) is BreakableBrickTile bbt) {
                bumpScale = bbt.BumpSize / 2;
                bumpOffset += bbt.BumpOffset;
            }

            bool kill = QuantumUtils.Decrement(ref blockBump->Lifetime);
            FP sizeAmount = FPMath.Sin(blockBump->Lifetime * f.DeltaTime / bumpDuration * FP.Pi);
            FPVector2 newSize = bumpScale;
            newSize.Y += FP._0_25 * sizeAmount / 3;

            collider->Shape.Box.Extents = newSize;
            transform->Position = blockBump->Origin + bumpOffset;
            transform->Position.Y += blockBump->IsDownwards ? (bumpScale.Y * 2 - newSize.Y) : newSize.Y;

            if (!blockBump->HasBumped) {
                Bump(f, transform->Position, blockBump->Owner, blockBump->AllowSelfDamage, !blockBump->IsDownwards, bumpScale.X, -bumpOffset.Y / 2);
                blockBump->HasBumped = true;
            }

            if (kill) {
                Kill(f, ref filter);
            }
        }

        public void Kill(Frame f, ref Filter filter) {
            var blockBump = filter.BlockBump;
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            stage.SetTileRelative(f, blockBump->Tile, blockBump->ResultTile);

            if (f.TryFindAsset(blockBump->Powerup, out var powerupPrototype)) {
                EntityRef newCoinItemEntity = f.Create(powerupPrototype);

                if (f.Unsafe.TryGetPointer(newCoinItemEntity, out CoinItem* coinItem)
                    && f.TryFindAsset(coinItem->Scriptable, out CoinItemAsset cia)) {
                    coinItem->SpawnChanceRaw = blockBump->SpawnChanceRaw;
                    coinItem->TotalSpawnChance = blockBump->TotalSpawnChance;

                    cia.InitializeFromBlockBump(f, newCoinItemEntity, ref filter);
                }
            }

            f.Destroy(filter.Entity);
        }

        public static void Bump(Frame f, FPVector2 position, EntityRef bumpee, bool allowSelfDamage, bool fromBelow, FP? width = null, FP? height = null) {
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

                f.Signals.OnEntityBumped(hit.Entity, position, bumpee, fromBelow);
            }
        }
    }
}