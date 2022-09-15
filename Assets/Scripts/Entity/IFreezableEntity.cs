using Fusion;

public interface IFreezableEntity {

    public bool IsCarryable { get; }
    public bool IsFlying { get; }
    public bool Frozen { get; set; }

    [Rpc]
    public void Freeze(FrozenCube cube);

    [Rpc]
    public void Unfreeze(byte reasonByte);

    public enum UnfreezeReason : byte {
        Other,
        Timer,
        Groundpounded,
        BlockBump,
        HitWall,
    }
}
