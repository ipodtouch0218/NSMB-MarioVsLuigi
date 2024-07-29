using Photon.Deterministic;

namespace Quantum {

    public unsafe class BlockBumpSystem : SystemMainThreadFilter<BlockBumpSystem.Filter> {
        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public BlockBump* BlockBump;
            public PhysicsCollider2D* Collider;
        }

        public override void Update(Frame f, ref Filter filter) {
            FP bumpSize = FP.FromString("0.35");
            FP bumpScale = FP._0_25;
            FP bumpDuration = FP._0_25;

            var blockBump = filter.BlockBump;
            var collider = filter.Collider;
            var transform = filter.Transform;

            bool kill = QuantumUtils.Decrement(ref blockBump->Lifetime);
            FP size = FPMath.Sin(((FP) blockBump->Lifetime / 60 / bumpDuration) * FP.Pi) * bumpScale;

            collider->Shape.Box.Extents = new FPVector2(FP._0_25 + size, FP._0_25 + size);
            transform->Position = blockBump->Origin + new FPVector2(0, size * (blockBump->IsDownwards ? -1 : 1));

            if (kill) {
                Kill(f, filter);
            }
        }

        public void Kill(Frame f, Filter filter) {
            var blockBump = filter.BlockBump;
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);

            stage.SetTileRelative(f, blockBump->TileX, blockBump->TileY, blockBump->ResultTile);

            if (blockBump->Powerup.IsValid) {
                EntityRef newPowerup = f.Create(blockBump->Powerup);
                if (f.Unsafe.TryGetPointer(newPowerup, out Powerup* powerup)) {
                    // Launch if downwards bump and theres a (solid) block below us
                    StageTileInstance tileInstance = stage.GetTileRelative(f, blockBump->TileX, blockBump->TileY - 1);
                    bool launch = blockBump->IsDownwards && tileInstance.GetWorldPolygons(f).Length != 0;

                    FP height = f.Get<PhysicsCollider2D>(newPowerup).Shape.Box.Extents.Y * 2;
                    FPVector2 origin = filter.Transform->Position + FPVector2.Down * FP._0_25;

                    var powerupScriptable = f.FindAsset(powerup->Scriptable);
                    if (powerupScriptable.State == PowerupState.MegaMushroom) {
                        powerup->Initialize(f, newPowerup, 90, origin + FPVector2.Up * FP._0_50, origin + FPVector2.Up * FP._0_50, false);
                    } else {
                        powerup->Initialize(f, newPowerup, 60, origin,
                            origin + (blockBump->IsDownwards ? FPVector2.Down * height : FPVector2.Up * FP._0_50), launch);
                    }
                }

                /*
                bool mega = SpawnPrefab == PrefabList.Instance.Powerup_MegaMushroom;
                Vector2 pos = (Vector2) transform.position + SpawnOffset;
                Vector2 animOrigin = pos;
                Vector2 animDestination;
                float pickupDelay = 0.75f;

                if (mega) {
                    animOrigin += (Vector2.up * 0.5f);
                    animDestination = animOrigin;
                    pickupDelay = 1.5f;

                } else if (IsDownwards) {
                    float blockSize = sRenderer.sprite.bounds.size.y * 0.5f;
                    animOrigin += (0.5f - blockSize) * Vector2.up;
                    animDestination = animOrigin + (Vector2.down * 0.5f);

                } else {
                    animDestination = animOrigin + (Vector2.up * 0.5f);
                }
                */
            }

            f.Destroy(filter.Entity);
        }
    }
}