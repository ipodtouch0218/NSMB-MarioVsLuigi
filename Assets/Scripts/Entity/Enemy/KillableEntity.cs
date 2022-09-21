using UnityEngine;

using Fusion;
using NSMB.Utils;

public abstract class KillableEntity : FreezableEntity, IPlayerInteractable {

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

    public override bool IsCarryable => iceCarryable;
    public override bool IsFlying => flying;

    [Networked] public NetworkBool Dead { get; set; }

    public bool collide = true, iceCarryable = true, flying;

    public BoxCollider2D hitbox;
    protected Animator animator;
    protected SpriteRenderer sRenderer;
    protected AudioSource audioSource;
    protected PhysicsEntity physics;

    #region Unity Methods
    public override void Awake() {
        base.Awake();
        hitbox = GetComponent<BoxCollider2D>();
        animator = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
        sRenderer = GetComponent<SpriteRenderer>();
        physics = GetComponent<PhysicsEntity>();
    }

    public override void FixedUpdateNetwork() {
        if (!GameManager.Instance || !body)
            return;

        Vector2 loc = body.position + hitbox.offset * transform.lossyScale;
        if (body && !Dead && !IsFrozen && !body.isKinematic && Utils.IsTileSolidAtTileLocation(Utils.WorldToTilemapPosition(loc)) && Utils.IsTileSolidAtWorldLocation(loc))
            SpecialKill(FacingRight, false, 0);
    }
    #endregion

    #region Unity Callbacks
    public void OnTriggerEnter2D(Collider2D collider) {
        KillableEntity entity = collider.GetComponentInParent<KillableEntity>();
        if (!collide || !Object.HasStateAuthority || !entity || entity.Dead)
            return;

        bool goRight = body.position.x > collider.attachedRigidbody.position.x;
        if (body.position.x == collider.attachedRigidbody.position.x) {
            goRight = body.position.y < collider.attachedRigidbody.position.y;
        }
        FacingRight = goRight;
    }
    #endregion

    #region Helper Methods
    public virtual void InteractWithPlayer(PlayerController player) {

        Vector2 damageDirection = (player.body.position - body.position).normalized;
        bool attackedFromAbove = Vector2.Dot(damageDirection, Vector2.up) > 0.5f && !player.onGround;

        if (!attackedFromAbove && player.State == Enums.PowerupState.BlueShell && player.crouching && !player.inShell) {
            FacingRight = damageDirection.x < 0;
        } else if (player.IsStarmanInvincible || player.inShell || player.sliding
            || (player.groundpound && player.State != Enums.PowerupState.MiniMushroom && attackedFromAbove)
            || player.State == Enums.PowerupState.MegaMushroom) {

            SpecialKill(player.body.velocity.x > 0, player.groundpound, player.StarCombo++);
        } else if (attackedFromAbove) {
            if (player.State == Enums.PowerupState.MiniMushroom) {
                if (player.groundpound) {
                    player.groundpound = false;
                    Kill();
                }
                player.bounce = true;
            } else {
                Kill();
                player.bounce = !player.groundpound;
            }
            player.PlaySound(Enums.Sounds.Enemy_Generic_Stomp);
            player.drill = false;

        } else if (player.IsDamageable) {
            player.Powerdown(false);
            FacingRight = damageDirection.x > 0;
        }
    }
    #endregion

    public override void Freeze(FrozenCube cube) {
        audioSource.Stop();
        PlaySound(Enums.Sounds.Enemy_Generic_Freeze);
        IsFrozen = true;
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

    public override void Unfreeze(UnfreezeReason reasonByte) {
        IsFrozen = false;
        animator.enabled = true;
        if (body)
            body.isKinematic = false;
        hitbox.enabled = true;
        audioSource.enabled = true;

        SpecialKill(false, false, 0);
    }

    public override void Bump(InteractableTile.InteractionDirection direction) {
        if (direction == InteractableTile.InteractionDirection.Down)
            return;

        SpecialKill(false, false, 0);
    }

    public virtual void Kill() {
        SpecialKill(false, false, 0);
    }

    public virtual void SpecialKill(bool right, bool groundpound, int combo) {
        if (Dead)
            return;

        Dead = true;

        body.constraints = RigidbodyConstraints2D.None;
        body.velocity = new(2f * (right ? 1 : -1), 2.5f);
        body.angularVelocity = 400f * (right ? 1 : -1);
        body.gravityScale = 1.5f;
        audioSource.enabled = true;
        animator.enabled = true;
        hitbox.enabled = false;
        animator.speed = 0;
        gameObject.layer = Layers.LayerHitsNothing;
        PlaySound(!IsFrozen ? COMBOS[Mathf.Min(COMBOS.Length - 1, combo)] : Enums.Sounds.Enemy_Generic_FreezeShatter);
        if (groundpound)
            Instantiate(Resources.Load("Prefabs/Particle/EnemySpecialKill"), body.position + Vector2.up * 0.5f, Quaternion.identity);

        Runner.Spawn(PrefabList.Instance.Obj_LooseCoin, body.position + hitbox.offset);
    }

    public void PlaySound(Enums.Sounds sound) {
        audioSource.PlayOneShot(sound.GetClip());
    }
}
