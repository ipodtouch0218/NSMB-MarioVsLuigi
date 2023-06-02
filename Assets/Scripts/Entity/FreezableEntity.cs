using Fusion;

using NSMB.Entities;

public abstract class FreezableEntity : BasicEntity {

    //---Networked Variables
    [Networked(OnChanged = nameof(OnIsFrozenChanged))] public NetworkBool IsFrozen { get; set; }

    //---Properties
    public abstract bool IsCarryable { get; }
    public abstract bool IsFlying { get; }

    public abstract void Freeze(FrozenCube cube);

    public abstract void Unfreeze(UnfreezeReason reasonByte);

    public virtual void OnIsFrozenChanged() { }

    public enum UnfreezeReason : byte {
        Other,
        Timer,
        Groundpounded,
        BlockBump,
        HitWall,
    }

    //---OnChangeds
    public static void OnIsFrozenChanged(Changed<FreezableEntity> changed) {
        FreezableEntity entity = changed.Behaviour;

        if (entity.IsFrozen)
            entity.PlaySound(Enums.Sounds.Enemy_Generic_Freeze);

        entity.OnIsFrozenChanged();
    }
}
