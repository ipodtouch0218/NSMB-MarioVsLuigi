using UnityEngine;

using Fusion;
using NSMB.Extensions;
using NSMB.Tiles;
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
    protected readonly ContactPoint2D[] ContactBuffer = new ContactPoint2D[32];

    //---Networked Variables
    [Networked(OnChanged = nameof(OnIsDeadChanged))] public NetworkBool IsDead { get; set; }
    [Networked] protected NetworkBool WasSpecialKilled { get; set; }
    [Networked] protected NetworkBool WasGroundpounded { get; set; }
    [Networked] protected byte ComboCounter { get; set; }

    //---Properties
    public override bool IsCarryable => iceCarryable;
    public override bool IsFlying => flying;

    //---Serialized Variables
    [SerializeField] protected bool iceCarryable = true;
    [SerializeField] protected bool flying = false;
    [SerializeField] protected bool collideWithOtherEnemies = true;
    [SerializeField] protected bool dieWhenInsideBlock = true;
    [SerializeField] protected bool flipSpriteRenderer = false;

    //---Components
    [SerializeField] public BoxCollider2D hitbox;
    [SerializeField] protected Animator animator;
    [SerializeField] protected LegacyAnimateSpriteRenderer legacyAnimation;
    [SerializeField] public SpriteRenderer sRenderer;
    [SerializeField] protected AudioSource audioSource;
    [SerializeField] protected PhysicsEntity physics;

    public override void OnValidate() {
        base.OnValidate();
        if (!hitbox) hitbox = GetComponent<BoxCollider2D>();
        if (!animator) animator = GetComponentInChildren<Animator>();
        if (!audioSource) audioSource = GetComponent<AudioSource>();
        if (!sRenderer) sRenderer = GetComponentInChildren<SpriteRenderer>();
        if (!legacyAnimation) legacyAnimation = GetComponentInChildren<LegacyAnimateSpriteRenderer>();
        if (!physics) physics = GetComponent<PhysicsEntity>();
    }

    public override void Spawned() {
        base.Spawned();
        DespawnEntity();
        OnIsActiveChanged();
    }

    public override void FixedUpdateNetwork() {
        base.FixedUpdateNetwork();
        if (!GameManager.Instance || !body || IsFrozen)
            return;

        if (!IsActive) {
            gameObject.layer = Layers.LayerHitsNothing;
            body.angularVelocity = 0;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.velocity = Vector2.zero;
            body.isKinematic = true;
            return;

        } else if (IsDead) {
            gameObject.layer = Layers.LayerHitsNothing;
            body.isKinematic = false;

            if (WasSpecialKilled) {
                body.angularVelocity = 400f * (FacingRight ? 1 : -1);
                body.constraints = RigidbodyConstraints2D.None;
            }
            return;
        } else {
            gameObject.layer = Layers.LayerEntity;
            body.isKinematic = false;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        if (collideWithOtherEnemies) {
            CheckForEntityCollisions();
        }

        if (dieWhenInsideBlock) {
            Vector2 loc = body.position + hitbox.offset * transform.lossyScale;
            if (!body.isKinematic && Utils.IsTileSolidAtWorldLocation(loc)) {
                SpecialKill(FacingRight, false, 0);
            }
        }
    }

    protected virtual void CheckForEntityCollisions() {

        int count = Runner.GetPhysicsScene2D().OverlapBox(body.position + hitbox.offset, hitbox.size, 0, CollisionBuffer);

        for (int i = 0; i < count; i++) {
            GameObject obj = CollisionBuffer[i].gameObject;

            if (obj == gameObject)
                continue;

            if (obj.GetComponent<KillableEntity>() is KillableEntity killable) {
                if (killable.IsDead || killable is PiranhaPlantController)
                    continue;

                bool goRight = body.position.x > killable.body.position.x;
                if (Mathf.Abs(body.position.x - killable.body.position.x) < 0.015f) {
                    if (Mathf.Abs(body.position.y - killable.body.position.y) < 0.015f) {
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
        if (IsDead)
            return;

        SpecialKill(false, false, 0);
    }

    public virtual void SpecialKill(bool right, bool groundpound, int combo) {
        if (IsDead)
            return;

        IsDead = true;
        WasSpecialKilled = true;
        WasGroundpounded = groundpound;
        ComboCounter = (byte) combo;
        FacingRight = right;

        body.constraints = RigidbodyConstraints2D.None;
        body.velocity = new(2f * (FacingRight ? 1 : -1), 2.5f);
        body.angularVelocity = 400f * (FacingRight ? 1 : -1);
        body.gravityScale = 1.5f;

        Runner.Spawn(PrefabList.Instance.Obj_LooseCoin, body.position + hitbox.offset);
    }

    public virtual void OnIsDeadChanged() {
        if (IsDead) {
            //death effects
            if (animator)
                animator.enabled = false;
            audioSource.enabled = true;

            if (WasSpecialKilled)
                PlaySound(!IsFrozen ? COMBOS[Mathf.Min(COMBOS.Length - 1, ComboCounter)] : Enums.Sounds.Enemy_Generic_FreezeShatter);

            if (WasGroundpounded)
                Instantiate(PrefabList.Instance.Particle_EnemySpecialKill, body.position + hitbox.offset, Quaternion.identity);

        } else {
            //undo death effects
            if (animator)
                animator.enabled = true;
        }
    }

    public void PlaySound(Enums.Sounds sound) {
        audioSource.PlayOneShot(sound);
    }

    //---BasicEntity overrides
    public override void OnIsActiveChanged() {
        if (IsActive) {
            if (sRenderer)
                sRenderer.enabled = true;
        } else {
            if (sRenderer)
                sRenderer.enabled = false;
        }
    }

    public override void OnFacingRightChanged() {
        sRenderer.flipX = FacingRight ^ flipSpriteRenderer;
    }

    public override void RespawnEntity() {
        if (IsActive)
            return;

        base.RespawnEntity();
        IsDead = false;
        IsFrozen = false;
        FacingRight = false;
        WasSpecialKilled = false;
        WasGroundpounded = false;
        ComboCounter = 0;
        //gameObject.layer = Layers.LayerEntity;
    }

    public override void DespawnEntity(object data = null) {
        base.DespawnEntity(data);
        IsDead = true;
    }

    //---IPlayerInteractable overrides
    public virtual void InteractWithPlayer(PlayerController player) {

        Vector2 damageDirection = (player.body.position - body.position).normalized;
        bool attackedFromAbove = Vector2.Dot(damageDirection, Vector2.up) > 0.5f && !player.IsOnGround;

        bool groundpounded = attackedFromAbove && player.HasGroundpoundHitbox && player.State != Enums.PowerupState.MiniMushroom;
        if (player.InstakillsEnemies || groundpounded) {
            SpecialKill(player.body.velocity.x > 0, player.IsGroundpounding, player.StarCombo++);
            return;
        }

        if (attackedFromAbove) {
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

            player.IsDrilling = false;

        } else if (player.IsCrouchedInShell) {
            FacingRight = damageDirection.x < 0;

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
    public override void BlockBump(BasicEntity bumper, Vector2Int tile, InteractableTile.InteractionDirection direction) {
        SpecialKill(false, false, 0);
    }

    //---FreezableEntity overrides
    public override void Freeze(FrozenCube cube) {
        audioSource.Stop();
        IsFrozen = true;
        if (animator)
            animator.enabled = false;
        if (legacyAnimation)
            legacyAnimation.enabled = false;

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
        if (animator)
            animator.enabled = true;
        if (body)
            body.isKinematic = false;
        hitbox.enabled = true;
        audioSource.enabled = true;

        SpecialKill(false, false, 0);
    }

    //---OnChangeds
    public static void OnIsDeadChanged(Changed<KillableEntity> changed) {
        changed.Behaviour.OnIsDeadChanged();
    }
}
