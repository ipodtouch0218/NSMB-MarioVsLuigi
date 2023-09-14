namespace NSMB.Entities.Player {
    public interface IPlayerInteractable {

        public void InteractWithPlayer(PlayerController player, PhysicsDataStruct.IContactStruct contact);

    }
}