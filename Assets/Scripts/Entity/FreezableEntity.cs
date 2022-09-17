using Fusion;

public abstract class FreezableEntity : BasicEntity {

    //---Networked Variables
    [Networked] public bool IsFrozen { get; set; }

    //---Properties
    public abstract bool IsCarryable { get; }
    public abstract bool IsFlying { get; }

    public abstract void Freeze(FrozenCube cube);

    public abstract void Unfreeze(byte reasonByte);

    public enum UnfreezeReason : byte {
        Other,
        Timer,
        Groundpounded,
        BlockBump,
        HitWall,
    }
}
