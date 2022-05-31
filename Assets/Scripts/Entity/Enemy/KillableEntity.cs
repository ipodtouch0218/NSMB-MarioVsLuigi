using UnityEngine;
using Photon.Pun;

public abstract class KillableEntity : MonoBehaviourPun, IFreezableEntity {

    public bool dead, frozen, left = true, collide = true, iceCarryable = true, flying;
    public Rigidbody2D body;
    protected BoxCollider2D hitbox;
    protected Animator animator;
    protected SpriteRenderer sRenderer;
    protected AudioSource audioSource;
    protected PhysicsEntity physics;

    bool IFreezableEntity.IsCarryable => iceCarryable;
    bool IFreezableEntity.IsFlying => flying;

    public void Start() {
        body = GetComponent<Rigidbody2D>();
        hitbox = GetComponent<BoxCollider2D>();
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
        sRenderer = GetComponent<SpriteRenderer>();
        physics = GetComponent<PhysicsEntity>();
    }

    public virtual void FixedUpdate() {
        if (!photonView || !GameManager.Instance || !photonView.IsMine)
            return;

        if (body && !dead && !frozen && !body.isKinematic && Utils.IsTileSolidAtWorldLocation(body.position + Vector2.up * 0.3f))
            photonView.RPC("SpecialKill", RpcTarget.All, left, false);
    }

    public void OnTriggerEnter2D(Collider2D collider) {
        if (!collide || !photonView.IsMine || !collider.GetComponentInParent<KillableEntity>())
            return;

        bool goLeft = body.position.x < collider.attachedRigidbody.position.x;
        if (body.position.x == collider.attachedRigidbody.position.x) {
            goLeft = body.position.y > collider.attachedRigidbody.position.y;
        }
        photonView.RPC("SetLeft", RpcTarget.All, goLeft);
    }

    public virtual void InteractWithPlayer(PlayerController player) {
        if (player.frozen)
            return;
        Vector2 damageDirection = (player.body.position - body.position).normalized;
        bool attackedFromAbove = Vector2.Dot(damageDirection, Vector2.up) > 0.5f && !player.onGround;

        if (!attackedFromAbove && player.state == Enums.PowerupState.BlueShell && player.crouching && !player.inShell) {
            photonView.RPC("SetLeft", RpcTarget.All, damageDirection.x > 0);
        } else if (player.invincible > 0 || player.inShell || player.sliding
            || ((player.groundpound || player.drill) && player.state != Enums.PowerupState.MiniMushroom && attackedFromAbove)
            || player.state == Enums.PowerupState.MegaMushroom) {

            photonView.RPC("SpecialKill", RpcTarget.All, player.body.velocity.x > 0, player.groundpound);
        } else if (attackedFromAbove) {
            if (player.state == Enums.PowerupState.MiniMushroom && !player.drill && !player.groundpound) {
                player.groundpound = false;
                player.bounce = true;
            } else {
                photonView.RPC("Kill", RpcTarget.All);
                player.groundpound = false;
                player.bounce = !player.drill;
            }
            player.photonView.RPC("PlaySound", RpcTarget.All, Enums.Sounds.Enemy_Generic_Stomp);
            player.drill = false;
        } else if (player.hitInvincibilityCounter <= 0) {
            player.photonView.RPC("Powerdown", RpcTarget.All, false);
            photonView.RPC("SetLeft", RpcTarget.All, damageDirection.x < 0);
        }
    }

    [PunRPC]
    public void SetLeft(bool left) {
        this.left = left;
        body.velocity = new Vector2(Mathf.Abs(body.velocity.x) * (left ? -1 : 1), body.velocity.y);
    }

    [PunRPC]
    public abstract void Kill();

    [PunRPC]
    public virtual void Freeze(int cube) {
        PlaySound(Enums.Sounds.Enemy_Generic_Freeze);
        frozen = true;
        animator.enabled = false;
        hitbox.enabled = false;
        if (body) {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0;
            body.isKinematic = true;
		}
    }

    [PunRPC]
    public virtual void Unfreeze() {
        frozen = false;
        animator.enabled = true;
        if (body)
            body.isKinematic = false;
        hitbox.enabled = true;
        audioSource.enabled = true;

        SpecialKill(false, false);
    }

    [PunRPC]
    public virtual void SpecialKill(bool right = true, bool groundpound = false) {
        if (dead)
            return;

        body.velocity = new Vector2(2.5f * (right ? 1 : -1), 2.5f);
        body.constraints = RigidbodyConstraints2D.None;
        body.angularVelocity = 400f * (right ? 1 : -1);
        body.gravityScale = 1.5f;
        audioSource.enabled = true;
        animator.enabled = true;
        hitbox.enabled = false;
        animator.speed = 0;
        gameObject.layer = LayerMask.NameToLayer("HitsNothing");
        dead = true;
        photonView.RPC("PlaySound", RpcTarget.All, !frozen ? Enums.Sounds.Enemy_Generic_Kick : Enums.Sounds.Enemy_Generic_FreezeShatter);
        if (groundpound)
            Instantiate(Resources.Load("Prefabs/Particle/EnemySpecialKill"), body.position + new Vector2(0, 0.5f), Quaternion.identity);
        
        if (PhotonNetwork.IsMasterClient)
            PhotonNetwork.InstantiateRoomObject("Prefabs/LooseCoin", body.position + new Vector2(0, 0.5f), Quaternion.identity);
    } 

    [PunRPC]
    public void PlaySound(Enums.Sounds sound) {
        audioSource.PlayOneShot(sound.GetClip());
    }
}
