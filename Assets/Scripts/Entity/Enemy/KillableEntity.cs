using System.Collections.Generic;
using UnityEngine;

using Photon.Pun;
using NSMB.Utils;

public abstract class KillableEntity : MonoBehaviourPun, IFreezableEntity, ICustomSerializeView {

    private static readonly float RESEND_RATE = 0.5f;

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

    public bool Frozen { get; set; } = false;
    public bool IsCarryable => iceCarryable;
    public bool IsFlying => flying;
    public bool Active { get; set; } = true;

    public bool dead, left = true, collide = true, iceCarryable = true, flying;
    public Rigidbody2D body;
    public BoxCollider2D hitbox;
    protected Animator animator;
    protected SpriteRenderer sRenderer;
    protected AudioSource audioSource;
    protected PhysicsEntity physics;

    private byte previousFlags;
    private double lastSendTimestamp;

    #region Pun Serialization
    public void Serialize(List<byte> buffer) {
        SerializationUtils.PackToByte(out byte flags, dead, left);

        bool forceResend = PhotonNetwork.Time - lastSendTimestamp > RESEND_RATE;

        if (flags != previousFlags || forceResend) {
            SerializationUtils.WriteByte(buffer, flags);

            previousFlags = flags;
            lastSendTimestamp = PhotonNetwork.Time;
        }
    }

    public void Deserialize(List<byte> buffer, ref int index, PhotonMessageInfo info) {
        SerializationUtils.UnpackFromByte(buffer, ref index, out bool[] flags);

        //dead = flags[0]; //synchronizing dead state causes issues with laggy players dying to dead enemies on their screen
        left = flags[1];
    }
    #endregion

    #region Unity Methods
    public virtual void Start() {
        body = GetComponent<Rigidbody2D>();
        hitbox = GetComponent<BoxCollider2D>();
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
        sRenderer = GetComponent<SpriteRenderer>();
        physics = GetComponent<PhysicsEntity>();
    }

    public virtual void FixedUpdate() {
        if (!(photonView?.IsMine ?? true) || !GameManager.Instance || !photonView.IsMine || !body)
            return;

        Vector2 loc = body.position + hitbox.offset * transform.lossyScale;
        if (body && !dead && !Frozen && !body.isKinematic && Utils.IsTileSolidAtTileLocation(Utils.WorldToTilemapPosition(loc)) && Utils.IsTileSolidAtWorldLocation(loc))
            photonView.RPC(nameof(SpecialKill), RpcTarget.All, left, false, 0);
    }
    #endregion

    #region Unity Callbacks
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
    #endregion

    #region Helper Methods
    public virtual void InteractWithPlayer(PlayerController player) {
        if (player.Frozen)
            return;

        Vector2 damageDirection = (player.body.position - body.position).normalized;
        bool attackedFromAbove = Vector2.Dot(damageDirection, Vector2.up) > 0.5f && !player.onGround;

        if (!attackedFromAbove && player.state == Enums.PowerupState.BlueShell && player.crouching && !player.inShell) {
            photonView.RPC(nameof(SetLeft), RpcTarget.All, damageDirection.x > 0);
        } else if (player.invincible > 0 || player.inShell || player.sliding
            || (player.groundpound && player.state != Enums.PowerupState.MiniMushroom && attackedFromAbove)
            || player.state == Enums.PowerupState.MegaMushroom) {

            photonView.RPC(nameof(SpecialKill), RpcTarget.All, player.body.velocity.x > 0, player.groundpound, player.StarCombo++);
        } else if (attackedFromAbove) {
            if (player.state == Enums.PowerupState.MiniMushroom) {
                if (player.groundpound) {
                    player.groundpound = false;
                    photonView.RPC(nameof(Kill), RpcTarget.All);
                }
                player.bounce = true;
            } else {
                photonView.RPC(nameof(Kill), RpcTarget.All);
                player.bounce = !player.groundpound;
            }
            player.photonView.RPC(nameof(PlaySound), RpcTarget.All, Enums.Sounds.Enemy_Generic_Stomp);
            player.drill = false;

        } else if (player.hitInvincibilityCounter <= 0) {
            player.photonView.RPC(nameof(PlayerController.Powerdown), RpcTarget.All, false);
            photonView.RPC(nameof(SetLeft), RpcTarget.All, damageDirection.x < 0);
        }
    }
    #endregion

    #region PunRPCs
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
    public virtual void Unfreeze(byte reasonByte) {
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
    #endregion
}
