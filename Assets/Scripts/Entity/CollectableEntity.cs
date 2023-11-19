using Fusion;

using NSMB.Entities.Player;

namespace NSMB.Entities.Collectable {
    public abstract class CollectableEntity : BasicEntity, IPlayerInteractable {

        //---Networked Variables
        [Networked] public PlayerController Collector { get; set; }

        public virtual void OnCollectedChanged() { }

        //---IPlayerInteractable overrides
        public abstract void InteractWithPlayer(PlayerController player, PhysicsDataStruct.IContactStruct contact = null);

        //---OnChangeds
        protected override void HandleRenderChanges(bool fillBuffer, ref NetworkBehaviourBuffer oldBuffer, ref NetworkBehaviourBuffer newBuffer) {
            base.HandleRenderChanges(fillBuffer, ref oldBuffer, ref newBuffer);

            foreach (var change in ChangesBuffer) {
                switch (change) {
                case nameof(Collector): OnCollectedChanged(); break;
                }
            }
        }
    }
}
