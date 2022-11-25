using UnityEngine;

using Fusion;
using NSMB.Extensions;
using NSMB.Utils;

public abstract class KillableEntity : FreezableEntity, IPlayerInteractable, IFireballInteractable {

    //---Static Variables
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
    protected readonly Collider2D[] CollisionBuffer = new Collider2D[32];

    //---Networked Variables
    [Networked] public NetworkBool IsDead { get; set; }

    //---Properties
    public override bool IsCarryable => iceCarryable;
    public override bool IsFlying => flying;

    //---Serialized Variables
    [SerializeField] protected bool iceCarryable = true;
    [SerializeField] protected bool flying = false;

    //---Components
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
        sRenderer = GetComponentInChildren<SpriteRenderer>();
        physics = GetComponent<PhysicsEntity>();
    }

    public override void FixedUpdateNetwork() {
        if (!GameManager.Instance || !body)
            return;

        if (!IsDead)
            CheckForEntityCollisions();

        Vector2 loc = body.position + hitbox.offset * transform.lossyScale;
        if (body && !IsDead && !IsFrozen && !body.isKinematic && Utils.IsTileSolidAtTileLocation(Utils.WorldToTilemapPosition(loc)) && Utils.IsTileSolidAtWorldLocation(loc)) {
            SpecialKill(FacingRight, false, 0);
        }
    }
    #endregion

    protected virtual void CheckForEntityCollisions() {

        int count = Runner.GetPhysicsScene2D().OverlapBox(body.position + hitbox.offset, hitbox.size, 0, CollisionBuffer);

        for (int i = 0; i < count; i++) {
            GameObject obj = CollisionBuffer[i].gameObject;

            if (obj == gameObject)
                continue;

            if (obj.GetComponent<KillableEntity>() is KillableEntity killable) {

                if (killable.IsDead)
                    continue;

                bool goRight = body.position.x > killable.body.position.x;
                if (Mathf.Abs(body.position.x - killable.body.position.x) < 0.001f) {
                    if (Mathf.Abs(body.position.y - killable.body.position.y) < 0.001f) {
                        goRight = Object.Id.Raw < killable.Object.Id.Raw;
                    } else {
                        goRight = body.position.y < killable.body.position.y;
                    }
                }

                FacingRight = goRight;
            }
        }
    }

    public virtual void Kill() {
        SpecialKill(false, false, 0);
    }

    public virtual void SpecialKill(bool right, bool groundpound, int combo) {
        if (IsDead)
            return;

        IsDead = true;

        body.constraints = RigidbodyConstraints2D.None;
        body.velocity = new(2f * (right ? 1 : -1), 2.5f);
        body.angularVelocity = 400f * (right ? 1 : -1);
        body.gravityScale = 1.5f;
        audioSource.enabled = true;
        animator.enabled = true;
        hitbox.enabled = false;
        animator.speed = 0;
        gameObject.layer = Layers.LayerHitsNothing;

        if (Runner.IsForward)
            PlaySound(!IsFrozen ? COMBOS[Mathf.Min(COMBOS.Length - 1, combo)] : Enums.Sounds.Enemy_Generic_FreezeShatter);

        if (groundpound)
            Instantiate(PrefabList.Instance.Particle_EnemySpecialKill, body.position + Vector2.up * 0.5f, Quaternion.identity);

        Runner.Spawn(PrefabList.Instance.Obj_LooseCoin, body.position + hitbox.offset);
    }

    public void PlaySound(Enums.Sounds sound) {
        audioSource.PlayOneShot(sound);
    }

    //---IPlayerInteractable overrides
    public virtual void InteractWithPlayer(PlayerController player) {

        Vector2 damageDirection = (player.body.position - body.position).normalized;
        bool attackedFromAbove = Vector2.Dot(damageDirection, Vector2.up) > 0.5f && !player.IsOnGround;

        if (!attackedFromAbove && player.State == Enums.PowerupState.BlueShell && player.IsCrouching && !player.IsInShell) {
            FacingRight = damageDirection.x < 0;
        } else if (player.IsStarmanInvincible || player.IsInShell || player.IsSliding
            || (player.IsGroundpounding && player.State != Enums.PowerupState.MiniMushroom && attackedFromAbove)
            || player.State == Enums.PowerupState.MegaMushroom) {

            SpecialKill(player.body.velocity.x > 0, player.IsGroundpounding, player.StarCombo++);
        } else if (attackedFromAbove) {
            if (player.State == Enums.PowerupState.MiniMushroom) {
                if (player.IsGroundpounding) {
                    player.IsGroundpounding = false;
                    Kill();
                }
                player.DoEntityBounce = true;
            } else {
                Kill();
                player.DoEntityBounce = !player.IsGroundpounding;
            }
            if (Runner.IsForward)
                player.PlaySound(Enums.Sounds.Enemy_Generic_Stomp);
            player.IsDrilling = false;

        } else if (player.IsDamageable) {
            player.Powerdown(false);
            FacingRight = damageDirection.x > 0;
        }
    }

    //---IFireballInteractable overrides
    public virtual bool InteractWithFireball(FireballMover fireball) {
        if (IsDead)
            return false;

        SpecialKill(fireball.FacingRight, false, 0);
        return true;
    }

    public virtual bool InteractWithIceball(FireballMover iceball) {
        if (IsDead)
            return false;

        if (!IsFrozen) {
            Runner.Spawn(PrefabList.Instance.Obj_FrozenCube, body.position, onBeforeSpawned: (runner, obj) => {
                FrozenCube cube = obj.GetComponent<FrozenCube>();
                cube.OnBeforeSpawned(this);
            });
        }
        return true;
    }

    //---IBlockBumpable overrides
    public override void BlockBump(BasicEntity bumper, Vector3Int tile, InteractableTile.InteractionDirection direction) {
        SpecialKill(false, false, 0);
    }

    //---FreezableEntity overrides
    public override void Freeze(FrozenCube cube) {
        audioSource.Stop();
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

    //---OnChangeds

}
