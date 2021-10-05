using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public abstract class HoldableEntity : KillableEntity {
    public GameObject holder;
    public Vector3 holderOffset;

    public void LateUpdate() {
        if (holder != null) {
            body.velocity = Vector2.zero;
            body.transform.position = holder.transform.position + holderOffset;
            return;
        }
    }

    [PunRPC]
    public abstract void Kick(bool fromLeft);

    [PunRPC]
    public abstract void Throw(bool facingLeft, bool crouching);

    [PunRPC]
    public void Pickup(int view) {
        if (holder != null) {
            return;
        }
        PhotonView holderView = PhotonView.Find(view);
        this.holder = holderView.gameObject;
        photonView.TransferOwnership(holderView.Owner);
    }
}
