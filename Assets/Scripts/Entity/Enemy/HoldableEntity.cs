﻿using UnityEngine;
using Photon.Pun;

public abstract class HoldableEntity : KillableEntity {
    public PlayerController holder, previousHolder;
    public Vector3 holderOffset;

    public bool canPlace = true;

    public void LateUpdate() {
        if (!holder) 
            return;

        body.velocity = Vector2.zero;
        body.position = transform.position = holder.transform.position + holderOffset;
        return;
    }

    public override void FixedUpdate() {
        if (!holder)
            base.FixedUpdate();
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
