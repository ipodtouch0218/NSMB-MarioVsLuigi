using Photon.Deterministic;
using Quantum;

public unsafe class MegaMushroomPowerupAsset : PowerupAsset {

    public FP GrowAnimationDuration = FP._1_50;

    public override int CountPlayersWithState(Frame f) {
        int playersWithPower = 0;
        foreach ((_, var otherPlayer) in f.Unsafe.GetComponentBlockIterator<MarioPlayer>()) {
            if (otherPlayer->CurrentPowerupState == PowerupState.MegaMushroom
                && otherPlayer->MegaMushroomStartFrames == 0
                && otherPlayer->MegaMushroomEndFrames == 0) {

                playersWithPower++;
            }
        }
        return playersWithPower;
    }

    public override void InitializeFromBlockBump(Frame f, EntityRef entity, ref BlockBumpSystem.Filter blockBumpFilter) {
        var blockBump = blockBumpFilter.BlockBump;
        BreakableBrickTile tile = (BreakableBrickTile) f.FindAsset(blockBump->StartTile);

        FPVector2 origin = blockBumpFilter.Transform->Position;
        origin.Y += tile.BumpSize.Y / 2;

        var coinItem = f.Unsafe.GetPointer<CoinItem>(entity);
        coinItem->InitializeBlockSpawn(f, entity, 90, origin, origin);
        coinItem->IgnorePlayerFrames = 85;
    }

    protected override unsafe void OnCollected(Frame f, EntityRef marioEntity) {
        var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
        var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);

        mario->MegaMushroomEndFrames = 0;
        mario->MegaMushroomStationaryEnd = false;
        mario->MegaMushroomStartFrames = (byte) (GrowAnimationDuration * f.UpdateRate);
        mario->IsSliding = false;
        mario->CurrentKnockback = KnockbackStrength.None;
        mario->KnockbackGetupFrames = 0;
        mario->TauntFrames = 0;
        if (f.Unsafe.TryGetPointer(mario->HeldEntity, out Holdable* holdable)) {
            holdable->DropWithoutThrowing(f, mario->HeldEntity);
        }
        if (marioPhysicsObject->IsTouchingGround) {
            mario->JumpState = JumpState.None;
        }
        marioPhysicsObject->IsFrozen = true;
        marioPhysicsObject->Velocity = FPVector2.Zero;
    }
}