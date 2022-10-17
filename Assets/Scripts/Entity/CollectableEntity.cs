using Fusion;

public abstract class CollectableEntity : BasicEntity, IPlayerInteractable {

    //---Networked Variables
    [Networked(OnChanged = nameof(CollectableEntity.OnCollectedChanged))] public PlayerController Collector { get; set; }

    public virtual void OnCollectedChanged() {}

    public static void OnCollectedChanged(Changed<CollectableEntity> changed) {
        changed.Behaviour.OnCollectedChanged();
    }

    //---IPlayerInteractable overrides
    public abstract void InteractWithPlayer(PlayerController player);
}
