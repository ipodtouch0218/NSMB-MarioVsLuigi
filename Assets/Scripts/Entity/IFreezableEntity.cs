using Photon.Pun;

public interface IFreezableEntity {

    public bool IsCarryable { get; }
    public bool IsFlying { get; }

    [PunRPC]
    public void Freeze(int cube);

    [PunRPC]
    public void Unfreeze();

}