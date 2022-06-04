using Photon.Pun;

public interface IFreezableEntity {

    public bool IsCarryable { get; }
    public bool IsFlying { get; }
    public bool Frozen { get; set; }

    [PunRPC]
    public void Freeze(int cube);

    [PunRPC]
    public void Unfreeze();

}