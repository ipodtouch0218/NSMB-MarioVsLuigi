using System.Collections.Generic;

using Photon.Pun;

public interface ICustomSerializeView {

    public bool Active { get; set; }

    void Serialize(List<byte> buffer);
    void Deserialize(List<byte> buffer, ref int index, PhotonMessageInfo info);

}