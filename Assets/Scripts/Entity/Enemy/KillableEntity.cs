using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public abstract class KillableEntity : MonoBehaviourPun {
    public bool dead;
    public bool frozen;
    public Rigidbody2D body;
    protected BoxCollider2D hitbox;
    protected Animator animator;
    protected SpriteRenderer sRenderer;
    protected AudioSource audioSource;
    protected PhysicsEntity physics;

    public bool dropcoin = true;

    public void Start() {
        body = GetComponent<Rigidbody2D>();
        hitbox = GetComponent<BoxCollider2D>();
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
        sRenderer = GetComponent<SpriteRenderer>();
        physics = GetComponent<PhysicsEntity>();
    }

    public virtual void InteractWithPlayer(PlayerController player) {
        if (player.frozen)
            return;
        Vector2 damageDirection = (player.body.position - body.position).normalized;
        bool attackedFromAbove = Vector2.Dot(damageDirection, Vector2.up) > 0.5f;

        if (player.invincible > 0 || player.inShell || player.sliding
            || ((player.groundpound || player.drill) && player.state != Enums.PowerupState.Mini && attackedFromAbove) 
            || player.state == Enums.PowerupState.Giant) {
            
            photonView.RPC("SpecialKill", RpcTarget.All, player.body.velocity.x > 0, player.groundpound);
            return;
        }
        if (attackedFromAbove) {
            if (player.state == Enums.PowerupState.Mini && !player.drill && !player.groundpound) {
                player.groundpound = false;
                player.bounce = true;
            } else {
                photonView.RPC("Kill", RpcTarget.All);
                player.groundpound = false;
                player.bounce = !player.drill;
            }
            player.photonView.RPC("PlaySound", RpcTarget.All, "enemy/goomba");
            player.drill = false;
            return;
        }
                
        player.photonView.RPC("Powerdown", RpcTarget.All, false);
    }

    [PunRPC]
    public abstract void Kill();

    [PunRPC]
    public virtual void Freeze() {
        frozen = true;
        animator.enabled = false;
        // Note: disabling hitbox doesn't work for some reason but I left the code here.
        hitbox.enabled = false;
        if (body) {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0;
            body.isKinematic = true;
		}
        dropcoin = false;
    }

    [PunRPC]
    public virtual void Unfreeze() {
        frozen = false;
        animator.enabled = true;
        body.isKinematic = false;
        dropcoin = true;
        hitbox.enabled = true;
    }

    [PunRPC]
    public virtual void SpecialKill(bool right = true, bool groundpound = false) {
        body.velocity = new Vector2(2.5f * (right ? 1 : -1), 2.5f);
        body.constraints = RigidbodyConstraints2D.None;
        body.angularVelocity = 400f * (right ? 1 : -1);
        body.gravityScale = 1.5f;
        hitbox.enabled = false;
        animator.speed = 0;
        gameObject.layer = LayerMask.NameToLayer("HitsNothing");
        dead = true;
        photonView.RPC("PlaySound", RpcTarget.All, "enemy/shell_kick");
        if (groundpound)
            Instantiate(Resources.Load("Prefabs/Particle/EnemySpecialKill"), body.position + new Vector2(0, 0.5f), Quaternion.identity);
        
        if (photonView.IsMine && dropcoin)
            PhotonNetwork.InstantiateRoomObject("Prefabs/LooseCoin", body.position + new Vector2(0, 0.5f), Quaternion.identity);
    } 
    [PunRPC]
    public void PlaySound(string sound) {
        audioSource.PlayOneShot((AudioClip) Resources.Load("Sound/" + sound));
    }
}
