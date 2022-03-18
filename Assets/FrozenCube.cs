using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

// maybe a better name for the script
public class FrozenCube : HoldableEntity
{
    public float throwSpeed = 10f;
    bool left;

    // TODO: when ice collides with something while after being thrown it breaks

    new void Start() {
        base.Start();
        hitbox = GetComponentInChildren<BoxCollider2D>();

        body.velocity = new Vector2(0, 0);
    }

	void FixedUpdate() {
        if (GameManager.Instance && GameManager.Instance.gameover) {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0;
            animator.enabled = false;
            body.isKinematic = true;
            return;
        }

        if (holder) {
            GetComponent<BoxCollider2D>().enabled = false;
        } else {
            GetComponent<BoxCollider2D>().enabled = true;
        }
    }
	// Start is called before the first frame update
	public override void InteractWithPlayer(PlayerController player) {
        Vector2 damageDirection = (player.body.position - body.position).normalized;
        bool attackedFromAbove = Vector2.Dot(damageDirection, Vector2.up) > 0f;
        if (holder)
            return;
        else if (player.groundpound && player.state != Enums.PowerupState.Mini && attackedFromAbove) {
            PhotonNetwork.Destroy(gameObject);
        }
        
        if (!holder) {
            if (player.state != Enums.PowerupState.Mini && !player.holding && player.running && !player.propeller && !player.flying && !player.crouching && !player.dead && !player.onLeft && !player.onRight && !player.doublejump && !player.triplejump) {
                photonView.RPC("Pickup", RpcTarget.All, player.photonView.ViewID);
                player.photonView.RPC("SetHolding", RpcTarget.All, photonView.ViewID);
            } else {
                photonView.RPC("Kick", RpcTarget.All, player.body.position.x < body.position.x, player.groundpound);
                player.photonView.RPC("SetHoldingOld", RpcTarget.All, photonView.ViewID);
                previousHolder = player;
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    [PunRPC]
    public override void Kick(bool fromLeft, bool groundpound) {
    }
    [PunRPC]
    public override void Throw(bool facingLeft, bool crouch) {
        if (holder == null)
            return;
        transform.position = new Vector2((holder.facingRight ? holder.transform.position.x + 0.1f : holder.transform.position.x - 0.1f), transform.position.y);
        
        previousHolder = holder;
        holder = null;
        
        photonView.TransferOwnership(PhotonNetwork.MasterClient);
        if (crouch) {
            body.velocity = new Vector2(2f * (facingLeft ? -1 : 1), body.velocity.y);
        } else {
            body.velocity = new Vector2(throwSpeed * (facingLeft ? -1 : 1), body.velocity.y);
        }
    }

    void OnTriggerEnter2D(Collider2D collider) {
        if ((photonView && !photonView.IsMine) || dead)
            return;

        GameObject obj = collider.gameObject;
        KillableEntity killa = obj.GetComponentInParent<KillableEntity>();
        switch (obj.tag) {
            case "koopa":
            case "bobomb":
            case "bulletbill":
            case "goomba":
            if (killa.dead)
                break;
            killa.photonView.RPC("SpecialKill", RpcTarget.All, killa.body.position.x > body.position.x, false);
            if (holder)
                photonView.RPC("SpecialKill", RpcTarget.All, killa.body.position.x < body.position.x, false);

            // Do ice breaking sound and particles here.

            PhotonNetwork.Destroy(gameObject);
            break;
            case "piranhaplant":
            if (killa.dead)
                break;
            killa.photonView.RPC("Kill", RpcTarget.All);
            if (holder)
                photonView.RPC("Kill", RpcTarget.All);

            break;
            case "coin":
            if (!holder && previousHolder)
                previousHolder.photonView.RPC("CollectCoin", RpcTarget.AllViaServer, obj.GetPhotonView().ViewID, new Vector3(obj.transform.position.x, collider.transform.position.y, 0));
            break;
            case "loosecoin":
            if (!holder && previousHolder) {
                Transform parent = obj.transform.parent;
                previousHolder.photonView.RPC("CollectCoin", RpcTarget.All, parent.gameObject.GetPhotonView().ViewID, parent.position);
            }
            break;
        }
    }

    [PunRPC]
    public override void Kill() {
        PhotonNetwork.Destroy(gameObject);
    }

    [PunRPC]
    public override void SpecialKill(bool right = true, bool groundpound = false) {
        base.SpecialKill(right, groundpound);
        if (holder)
            holder.holding = null;
        holder = null;
    }
}