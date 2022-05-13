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
        body.position = transform.position = holder.transform.position + holderOffset;
        return;
    }

    public override void FixedUpdate() {
        if (dead || !photonView || !GameManager.Instance || !photonView.IsMine)
            return;

        if (body && !holder && !body.isKinematic && Utils.IsTileSolidAtWorldLocation(body.position + Vector2.up * 0.3f))
            photonView.RPC("SpecialKill", RpcTarget.All, left, false);
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
