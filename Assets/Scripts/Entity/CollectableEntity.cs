using Fusion;

public abstract class CollectableEntity : BasicEntity, IPlayerInteractable {

    [Networked(OnChanged = "OnCollectedChanged")] public NetworkBool IsCollected { get; set; } = false;

    public abstract void InteractWithPlayer(PlayerController player);

    public static void OnCollectedChanged(Changed<CollectableEntity> changed) {
        changed.Behaviour.OnCollectedChanged();
    }

    public virtual void OnCollectedChanged() {

    }
}
