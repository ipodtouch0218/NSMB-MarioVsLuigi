using Fusion;

using NSMB.Entities.Player;

namespace NSMB.Entities.Collectable {
    public abstract class CollectableEntity : BasicEntity, IPlayerInteractable {

        //---Networked Variables
        [Networked(OnChanged = nameof(OnCollectedChanged))] public PlayerController Collector { get; set; }

        public virtual void OnCollectedChanged() { }

        public static void OnCollectedChanged(Changed<CollectableEntity> changed) {
            changed.Behaviour.OnCollectedChanged();
        }

        //---IPlayerInteractable overrides
        public abstract void InteractWithPlayer(PlayerController player, PhysicsDataStruct.IContactStruct contact = null);
    }
}
