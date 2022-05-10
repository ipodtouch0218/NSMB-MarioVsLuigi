using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public abstract class HoldableEntity : KillableEntity {
    public PlayerController holder, previousHolder;
    public Vector3 holderOffset;

    public void LateUpdate() {
        if (!holder) 
            return;

        body.velocity = Vector2.zero;
        transform.position = body.position = holder.transform.position + holderOffset;
        return;
    }

    [PunRPC]
    public abstract void Kick(bool fromLeft, float kickFactor, bool groundpound);

    [PunRPC]
    public abstract void Throw(bool facingLeft, bool crouching);

    [PunRPC]
    public void Pickup(int view) {
        if (holder) 
            return;

        PhotonView holderView = PhotonView.Find(view);
        holder = holderView.gameObject.GetComponent<PlayerController>();
        previousHolder = holder;
        photonView.TransferOwnership(holderView.Owner);
    }
}
