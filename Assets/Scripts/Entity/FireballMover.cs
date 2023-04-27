using UnityEngine;

using Fusion;
using NSMB.Extensions;
using NSMB.Tiles;
using NSMB.Utils;

[RequireComponent(typeof(NetworkRigidbody2D), typeof(PhysicsEntity))]
[OrderAfter(typeof(PlayerController))]
public class FireballMover : BasicEntity, IPlayerInteractable, IFireballInteractable {

    //---Static Variables
    private static readonly Collider2D[] CollisionBuffer = new Collider2D[16];

    //---Networked Variables
    [Networked] public PlayerController Owner { get; set; }
    [Networked] private float CurrentSpeed { get; set; }
    [Networked] public NetworkBool AlreadyBounced { get; set; }
    [Networked] public NetworkBool IsIceball { get; set; }
    [Networked(OnChanged = nameof(OnBreakEffectAnimCounterChanged))] public byte BreakEffectAnimCounter { get; set; }

    //---Serialized Variables
    [SerializeField] private ParticleSystem iceBreak, fireBreak, iceTrail, fireTrail;
    [SerializeField] private GameObject iceGraphics, fireGraphics;
    [SerializeField] private float fireSpeed = 6.25f, iceSpeed = 4.25f;
    [SerializeField] private float bounceHeight = 6.75f, terminalVelocity = 6.25f;

    //---Components
    [SerializeField] private PhysicsEntity physics;
    [SerializeField] private NetworkRigidbody2D nrb;
    [SerializeField] private SpriteRenderer[] renderers;
    [SerializeField] private BoxCollider2D hitbox;

    public override void OnValidate() {
        base.OnValidate();
        if (!physics) physics = GetComponent<PhysicsEntity>();
        if (!nrb) nrb = GetComponent<NetworkRigidbody2D>();
        if (!hitbox) hitbox = GetComponent<BoxCollider2D>();
        if ((renderers?.Length ?? 0) == 0) renderers = GetComponentsInChildren<SpriteRenderer>();
    }

    public void Initialize(PlayerController owner, Vector2 spawnpoint, bool ice, bool right) {
        //vars
        IsActive = true;
        IsIceball = ice;
        FacingRight = right;
        AlreadyBounced = false;
        Owner = owner;

        foreach (SpriteRenderer r in renderers)
            r.flipX = FacingRight;

        //speed
        body.gravityScale = IsIceball ? 2.2f : 4.4f;
        if (IsIceball) {
            CurrentSpeed = iceSpeed + Mathf.Abs(owner.body.velocity.x / 3f);
        } else {
            CurrentSpeed = fireSpeed;
        }

        //physics
        nrb.TeleportToPosition(spawnpoint, Vector3.zero);
        body.simulated = true;
        body.velocity = new(CurrentSpeed * (FacingRight ? 1 : -1), -CurrentSpeed);
    }

    public override void Spawned() {
        base.Spawned();

        body.isKinematic = true;
        iceGraphics.SetActive(false);
        fireGraphics.SetActive(false);

        transform.SetParent(GameManager.Instance.objectPoolParent.transform);
        GameManager.Instance.PooledFireballs.Add(this);
    }

    public override void FixedUpdateNetwork() {
        body.isKinematic = !IsActive;
        hitbox.enabled = IsActive;

        if (!IsActive)
            return;

        if (GameManager.Instance && GameManager.Instance.GameEnded) {
            body.velocity = Vector2.zero;
            foreach (Animation anim in GetComponentsInChildren<Animation>())
                anim.enabled = false;
            body.simulated = false;
            return;
        }

        if (body.position.y < GameManager.Instance.LevelMinY) {
            DespawnEntity();
            return;
        }

        if (!HandleCollision())
            return;

        if (!CheckForEntityCollision())
            return;

        body.velocity = new(CurrentSpeed * (FacingRight ? 1 : -1), Mathf.Max(-terminalVelocity, body.velocity.y));
    }

    //---Helper Methods
    private bool HandleCollision() {
        if (!IsActive)
            return false;

        PhysicsEntity.PhysicsDataStruct data = physics.UpdateCollisions();

        if (data.OnGround && !AlreadyBounced) {
            float boost = bounceHeight * Mathf.Abs(Mathf.Sin(physics.Data.FloorAngle * Mathf.Deg2Rad)) * 1.25f;
            if (Mathf.Sign(data.FloorAngle) != Mathf.Sign(body.velocity.x))
                boost = 0;

            body.velocity = new(body.velocity.x, bounceHeight + boost);
        } else if (IsIceball && body.velocity.y > 0.1f)  {
            AlreadyBounced = true;
        }
        bool breaking = data.HitLeft || data.HitRight || data.HitRoof || (data.OnGround && AlreadyBounced && body.velocity.y < 1f);
        if (breaking) {
            DespawnEntity();
            return false;
        }

        if (Utils.IsTileSolidAtWorldLocation(body.position)) {
            DespawnEntity();
            return false;
        }

        return true;
    }

    private bool CheckForEntityCollision() {
        if (!IsActive)
            return false;

        int count = Runner.GetPhysicsScene2D().OverlapBox(body.position + physics.currentCollider.offset, ((BoxCollider2D) physics.currentCollider).size, 0, default, CollisionBuffer);

        for (int i = 0; i < count; i++) {
            GameObject collidedObject = CollisionBuffer[i].gameObject;

            //don't interact with ourselves.
            if (CollisionBuffer[i].attachedRigidbody == body)
                continue;

            if (collidedObject.GetComponentInParent<IFireballInteractable>() is IFireballInteractable interactable) {
                //don't interact with our owner
                if (interactable is PlayerController player && player == Owner)
                    continue;

                bool result = IsIceball ? interactable.InteractWithIceball(this) : interactable.InteractWithFireball(this);
                if (result) {
                    //true = interacted & destroy.
                    DespawnEntity();
                    return false;
                }
            }
        }

        return true;
    }

    //---BasicEntity overrides
    public override void DespawnEntity(object data = null) {
        if (IsActive && (data is not bool dontPlayEffect || !dontPlayEffect))
            BreakEffectAnimCounter++;

        IsActive = false;
        body.velocity = Vector2.zero;
        body.simulated = false;
    }

    public override void OnIsActiveChanged() {
        if (IsActive) {
            //activate graphics and particles
            bool ice = IsIceball;

            if (ice) {
                iceTrail.Play();
                fireTrail.Stop();
            } else {
                fireTrail.Play();
                iceTrail.Stop();
            }
            iceGraphics.SetActive(ice);
            fireGraphics.SetActive(!ice);
        } else {
            //disable graphics & trail, but play poof fx
            iceGraphics.SetActive(false);
            fireGraphics.SetActive(false);
            iceTrail.Stop();
            fireTrail.Stop();
        }
    }

    //---IPlayerInteractable overrides
    public void InteractWithPlayer(PlayerController player) {
        // If we're not active, don't collide.
        if (!IsActive)
            return;

        // Check if they own us. If so, don't collide.
        if (Owner == player)
            return;

        // If they have knockback invincibility, don't collide.
        if (!player.DamageInvincibilityTimer.ExpiredOrNotRunning(Runner))
            return;

        // Should do damage checks
        if (player.IsStarmanInvincible || player.data.Team != Owner.data.Team) {

            // Player state checks
            switch (player.State) {
            case Enums.PowerupState.MegaMushroom: {
                return;
            }
            case Enums.PowerupState.MiniMushroom: {
                player.Death(false, false);
                return;
            }
            case Enums.PowerupState.BlueShell: {
                if (IsIceball && (player.IsInShell || player.IsCrouching || player.IsGroundpounding))
                    player.ShellSlowdownTimer = TickTimer.CreateFromSeconds(Runner, 0.65f);
                return;
            }
            }

            // Collision is a GO
            if (IsIceball) {
                // iceball
                if (!player.IsFrozen) {
                    Runner.Spawn(PrefabList.Instance.Obj_FrozenCube, body.position, onBeforeSpawned: (runner, obj) => {
                        FrozenCube cube = obj.GetComponent<FrozenCube>();
                        cube.OnBeforeSpawned(player);
                    });
                }
            } else {
                // fireball
                player.DoKnockback(FacingRight, 1, true, Object);
            }
        }

        // Destroy ourselves.
        DespawnEntity();
    }

    //---IFireballInteractable overrides
    public bool InteractWithFireball(FireballMover fireball) {
        if (!IsActive || !fireball.IsActive)
            return false;

        //fire + ice = both destroy
        if (IsIceball) {
            fireball.DespawnEntity();
            return true;
        }
        return false;
    }

    public bool InteractWithIceball(FireballMover iceball) {
        if (!IsActive || !iceball.IsActive)
            return false;

        //fire + ice = both destroy
        if (!IsIceball) {
            iceball.DespawnEntity();
            return true;
        }
        return false;
    }

    //---IBlockBumpable overrides
    public override void BlockBump(BasicEntity bumper, Vector2Int tile, InteractableTile.InteractionDirection direction) {
        //do nothing when bumped
    }

    //---OnChangeds
    public static void OnBreakEffectAnimCounterChanged(Changed<FireballMover> changed) {
        FireballMover fireball = changed.Behaviour;

        //dont play particles below the killplane
        if (fireball.body.position.y < GameManager.Instance.LevelMinY)
            return;

        if (fireball.IsIceball) {
            fireball.iceBreak.Play();
            fireball.sfx.PlayOneShot(Enums.Sounds.Powerup_Iceball_Break);
        } else {
            fireball.fireBreak.Play();
            fireball.sfx.PlayOneShot(Enums.Sounds.Powerup_Fireball_Break);
        }
    }
}
