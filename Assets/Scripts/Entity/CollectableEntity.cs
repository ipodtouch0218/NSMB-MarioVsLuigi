using Fusion;

public abstract class CollectableEntity : BasicEntity, IPlayerInteractable {

    [Networked(OnChanged = "OnCollect")] public NetworkBool IsCollected { get; set; } = false;

    public static void OnCollect(Changed<CollectableEntity> entity) { }

    public abstract void InteractWithPlayer(PlayerController player);
}
