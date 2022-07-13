using UnityEngine;
using Photon.Pun;
using NSMB.Utils;

public abstract class KillableEntity : MonoBehaviourPun, IFreezableEntity {

    public bool Frozen { get; set; }

    public bool dead, left = true, collide = true, iceCarryable = true, flying;
    public Rigidbody2D body;
    public BoxCollider2D hitbox;
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
        if (!(photonView?.IsMine ?? true) || !GameManager.Instance || !photonView.IsMine)
            return;

        if (body && !dead && !Frozen && !body.isKinematic && Utils.IsTileSolidAtWorldLocation(body.position + hitbox.offset * transform.lossyScale))
            photonView.RPC("SpecialKill", RpcTarget.All, left, false, 0);
    }

    public void OnTriggerEnter2D(Collider2D collider) {
        KillableEntity entity = collider.GetComponentInParent<KillableEntity>();
        if (!collide || !photonView.IsMine || !entity || entity.dead)
            return;

        bool goLeft = body.position.x < collider.attachedRigidbody.position.x;
        if (body.position.x == collider.attachedRigidbody.position.x) {
            goLeft = body.position.y > collider.attachedRigidbody.position.y;
        }
        photonView.RPC("SetLeft", RpcTarget.All, goLeft);
    }

    public virtual void InteractWithPlayer(PlayerController player) {
        if (player.Frozen)
            return;
        Vector2 damageDirection = (player.body.position - body.position).normalized;
        bool attackedFromAbove = Vector2.Dot(damageDirection, Vector2.up) > 0.5f && !player.onGround;

        if (!attackedFromAbove && player.state == Enums.PowerupState.BlueShell && player.crouching && !player.inShell) {
            photonView.RPC("SetLeft", RpcTarget.All, damageDirection.x > 0);
        } else if (player.invincible > 0 || player.inShell || player.sliding
            || ((player.groundpound || player.drill) && player.state != Enums.PowerupState.MiniMushroom && attackedFromAbove)
            || player.state == Enums.PowerupState.MegaMushroom) {

            photonView.RPC("SpecialKill", RpcTarget.All, player.body.velocity.x > 0, player.groundpound, 0);
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
    public virtual void Freeze(int cube) {
        audioSource.Stop();
        PlaySound(Enums.Sounds.Enemy_Generic_Freeze);
        Frozen = true;
        animator.enabled = false;
        foreach (BoxCollider2D hitboxes in GetComponentsInChildren<BoxCollider2D>()) {
            hitboxes.enabled = false;
        }
        if (body) {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0;
            body.isKinematic = true;
        }
    }

    [PunRPC]
    public virtual void Unfreeze() {
        Frozen = false;
        animator.enabled = true;
        if (body)
            body.isKinematic = false;
        hitbox.enabled = true;
        audioSource.enabled = true;

        SpecialKill(false, false, 0);
    }

    [PunRPC]
    public virtual void Kill() {
        SpecialKill(false, false, 0);
    }

    private static readonly Enums.Sounds[] COMBOS = {
        Enums.Sounds.Enemy_Shell_Kick,
        Enums.Sounds.Enemy_Shell_Combo1,
        Enums.Sounds.Enemy_Shell_Combo2,
        Enums.Sounds.Enemy_Shell_Combo3,
        Enums.Sounds.Enemy_Shell_Combo4,
        Enums.Sounds.Enemy_Shell_Combo5,
        Enums.Sounds.Enemy_Shell_Combo6,
        Enums.Sounds.Enemy_Shell_Combo7,
    };

    [PunRPC]
    public virtual void SpecialKill(bool right, bool groundpound, int combo) {
        if (dead)
            return;

        dead = true;

        body.constraints = RigidbodyConstraints2D.None;
        body.velocity = new(2f * (right ? 1 : -1), 2.5f);
        body.angularVelocity = 400f * (right ? 1 : -1);
        body.gravityScale = 1.5f;
        audioSource.enabled = true;
        animator.enabled = true;
        hitbox.enabled = false;
        animator.speed = 0;
        gameObject.layer = LayerMask.NameToLayer("HitsNothing");
        PlaySound(!Frozen ? COMBOS[Mathf.Min(COMBOS.Length - 1, combo)] : Enums.Sounds.Enemy_Generic_FreezeShatter);
        if (groundpound)
            Instantiate(Resources.Load("Prefabs/Particle/EnemySpecialKill"), body.position + Vector2.up * 0.5f, Quaternion.identity);

        if (PhotonNetwork.IsMasterClient)
            PhotonNetwork.InstantiateRoomObject("Prefabs/LooseCoin", body.position + Vector2.up * 0.5f, Quaternion.identity);

        body.velocity = new(2f * (right ? 1 : -1), 2.5f);
    }

    [PunRPC]
    public void PlaySound(Enums.Sounds sound) {
        audioSource.PlayOneShot(sound.GetClip());
    }
}
