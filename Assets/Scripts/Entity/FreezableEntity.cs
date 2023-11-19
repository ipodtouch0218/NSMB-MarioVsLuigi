using UnityEngine;

using Fusion;
using NSMB.Entities;

public abstract class FreezableEntity : BasicEntity {

    //---Networked Variables
    [Networked] public NetworkBool IsFrozen { get; set; }

    //---Properties
    public abstract bool IsCarryable { get; }
    public abstract bool IsFlying { get; }
    public abstract Vector2 FrozenSize { get; }
    public abstract Vector2 FrozenOffset { get; }

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
    protected override void HandleRenderChanges(bool fillBuffer, ref NetworkBehaviourBuffer oldBuffer, ref NetworkBehaviourBuffer newBuffer) {
        base.HandleRenderChanges(fillBuffer, ref oldBuffer, ref newBuffer);

        foreach (var change in ChangesBuffer) {
            switch (change) {
            case nameof(IsFrozen): OnIsFrozenChanged(); break;
            }
        }
    }
}
