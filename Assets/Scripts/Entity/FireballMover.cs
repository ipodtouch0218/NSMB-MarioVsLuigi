using UnityEngine;

using Fusion;
using NSMB.Extensions;
using NSMB.Utils;

[RequireComponent(typeof(NetworkRigidbody2D), typeof(PhysicsEntity))]
public class FireballMover : BasicEntity, IPlayerInteractable, IFireballInteractable {

    //---Static Variables
    private static readonly Collider2D[] CollisionBuffer = new Collider2D[16];

    //---Networked Variables
    [Networked] public PlayerController Owner { get; set; }
    [Networked] private float CurrentSpeed { get; set; }
    [Networked] public NetworkBool AlreadyBounced { get; set; }
    [Networked] public NetworkBool IsIceball { get; set; }
    [Networked] public NetworkBool PlayBreakEffect { get; set; }
    [Networked(OnChanged = nameof(OnIsActiveChanged))] public NetworkBool IsActive { get; set; }

    //---Serialized Variables
    [SerializeField] private ParticleSystem iceBreak, fireBreak, iceTrail, fireTrail;
    [SerializeField] private GameObject iceGraphics, fireGraphics;
    [SerializeField] private float fireSpeed = 6.25f, iceSpeed = 4.25f;
    [SerializeField] private float bounceHeight = 6.75f, terminalVelocity = 6.25f;

    //---Components
    private PhysicsEntity physics;
    private NetworkRigidbody2D nrb;
    private SpriteRenderer[] renderers;
    private BoxCollider2D hitbox;

    public override void Awake() {
        base.Awake();
        physics = GetComponent<PhysicsEntity>();
        nrb = GetComponent<NetworkRigidbody2D>();
        hitbox = GetComponent<BoxCollider2D>();
        renderers = GetComponentsInChildren<SpriteRenderer>();
    }

    public void Initialize(PlayerController owner, Vector2 spawnpoint, bool ice, bool right) {
        //vars
        IsActive = true;
        IsIceball = ice;
        PlayBreakEffect = true;
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

        if (GameManager.Instance && GameManager.Instance.gameover) {
            body.velocity = Vector2.zero;
            foreach (Animation anim in GetComponentsInChildren<Animation>())
                anim.enabled = false;
            body.simulated = false;
            return;
        }

        if (body.position.y < GameManager.Instance.LevelMinY) {
            Destroy();
            return;
        }

        if (!HandleCollision())
            return;

        if (!CheckForEntityCollision())
            return;

        body.velocity = new(CurrentSpeed * (FacingRight ? 1 : -1), Mathf.Max(-terminalVelocity, body.velocity.y));
    }

    public override void Destroy(DestroyCause cause = DestroyCause.None) {
        IsActive = false;
        body.velocity = Vector2.zero;
        body.isKinematic = true;
    }

    //---Helper Methods
    private bool HandleCollision() {
        if (!IsActive)
            return false;

        physics.UpdateCollisions();

        if (physics.OnGround && !AlreadyBounced) {
            float boost = bounceHeight * Mathf.Abs(Mathf.Sin(physics.FloorAngle * Mathf.Deg2Rad)) * 1.25f;
            if (Mathf.Sign(physics.FloorAngle) != Mathf.Sign(body.velocity.x))
                boost = 0;

            body.velocity = new(body.velocity.x, bounceHeight + boost);
        } else if (IsIceball && body.velocity.y > 0.1f)  {
            AlreadyBounced = true;
        }
        bool breaking = physics.HitLeft || physics.HitRight || physics.HitRoof || (physics.OnGround && AlreadyBounced && body.velocity.y < 1f);
        if (breaking) {
            Destroy();
            return false;
        }

        if (Utils.IsTileSolidAtWorldLocation(body.position)) {
            Destroy();
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
                    Destroy();
                    return false;
                }
            }
        }

        return true;
    }

    //---IPlayerInteractable overrides
    public void InteractWithPlayer(PlayerController player) {
        //If we're not active, don't collide.
        if (!IsActive)
            return;

        //Check if they own us. If so, don't collide.
        if (Owner == player)
            return;

        //If they have knockback invincibility, don't collide.
        if (!player.DamageInvincibilityTimer.ExpiredOrNotRunning(Runner))
            return;

        //Starman Check
        if (player.IsStarmanInvincible)
            return;

        //Player state checks
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

        //Collision is a GO
        if (IsIceball) {
            //iceball
            if (!player.IsFrozen) {
                Runner.Spawn(PrefabList.Instance.Obj_FrozenCube, body.position, onBeforeSpawned: (runner, obj) => {
                    FrozenCube cube = obj.GetComponent<FrozenCube>();
                    cube.OnBeforeSpawned(player);
                });
            }
        } else {
            //fireball
            //TODO: damage source?
            player.DoKnockback(FacingRight, 1, true, gameObject);
        }

        //Destroy ourselves.
        Destroy();
    }

    //---IFireballInteractable overrides
    public bool InteractWithFireball(FireballMover fireball) {
        if (!IsActive || !fireball.IsActive)
            return false;

        //fire + ice = both destroy
        if (IsIceball) {
            fireball.Destroy();
            return true;
        }
        return false;
    }

    public bool InteractWithIceball(FireballMover iceball) {
        if (!IsActive || !iceball.IsActive)
            return false;

        //fire + ice = both destroy
        if (!IsIceball) {
            iceball.Destroy();
            return true;
        }
        return false;
    }

    //---IBlockBumpable overrides
    public override void BlockBump(BasicEntity bumper, Vector3Int tile, InteractableTile.InteractionDirection direction) {
        //do nothing when bumped
    }

    //---OnChanged callbacks
    public static void OnIsActiveChanged(Changed<FireballMover> changed) {
        FireballMover mover = changed.Behaviour;

        if (mover.IsActive) {
            //activate graphics and particles
            bool ice = mover.IsIceball;

            if (ice) {
                mover.iceTrail.Play();
                mover.fireTrail.Stop();
            } else {
                mover.fireTrail.Play();
                mover.iceTrail.Stop();
            }
            mover.iceGraphics.SetActive(ice);
            mover.fireGraphics.SetActive(!ice);
        } else {
            //disable graphics & trail, but play poof fx
            mover.iceGraphics.SetActive(false);
            mover.fireGraphics.SetActive(false);
            mover.iceTrail.Stop();
            mover.fireTrail.Stop();

            //dont play particles below the killplane
            if (mover.body.position.y < GameManager.Instance.LevelMinY)
                return;

            //or if the killer said so
            if (!mover.PlayBreakEffect)
                return;

            if (mover.IsIceball) {
                mover.iceBreak.Play();
                mover.sfx.PlayOneShot(Enums.Sounds.Powerup_Iceball_Break);
            } else {
                mover.fireBreak.Play();
                mover.sfx.PlayOneShot(Enums.Sounds.Powerup_Fireball_Break);
            }
        }
    }
}
