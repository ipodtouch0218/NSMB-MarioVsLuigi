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
        Vector3 newLoc = holder.transform.position + holderOffset;
        if (Utils.IsTileSolidAtWorldLocation(newLoc))
            newLoc.x = body.position.x;

        body.position = newLoc;
        return;
    }

    [PunRPC]
    public abstract void Kick(bool fromLeft, bool groundpound);

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
