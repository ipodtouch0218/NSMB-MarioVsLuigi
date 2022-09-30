using UnityEngine;

using Fusion;

public class FireballMover : BasicEntity, IPlayerInteractable, IFireballInteractable {

    //---Networked Variables
    [Networked] public NetworkBool BreakOnImpact { get; set; }
    [Networked(Default = nameof(speed))] private float MoveSpeed { get; set; }

    //---Public Variables
    public GameObject wallHitPrefab;

    //---Serialized Variables
    [SerializeField] private ParticleSystem particles;
    [SerializeField] private float speed = 3f, bounceHeight = 4.5f, terminalVelocity = 6.25f;
    [SerializeField] public bool isIceball;

    //---Private Variables
    private PhysicsEntity physics;

    public void OnBeforeSpawned(PlayerController owner, bool right) {
        FacingRight = right;

        if (isIceball)
            MoveSpeed += Mathf.Abs(owner.body.velocity.x / 3f);
    }

    public override void Awake() {
        base.Awake();
        physics = GetComponent<PhysicsEntity>();
    }

    public override void Spawned() {
        base.Spawned();
        body.velocity = new(MoveSpeed * (FacingRight ? 1 : -1), -MoveSpeed);
    }

    public override void FixedUpdateNetwork() {
        if (GameManager.Instance && GameManager.Instance.gameover) {
            body.velocity = Vector2.zero;
            GetComponent<Animator>().enabled = false;
            body.isKinematic = true;
            return;
        }

        if (!HandleCollision())
            return;

        if (!CheckForEntityCollision())
            return;

        body.velocity = new(MoveSpeed * (FacingRight ? 1 : -1), Mathf.Max(-terminalVelocity, body.velocity.y));
    }

    private bool HandleCollision() {
        physics.UpdateCollisions();

        if (physics.onGround && !BreakOnImpact) {
            float boost = bounceHeight * Mathf.Abs(Mathf.Sin(physics.floorAngle * Mathf.Deg2Rad)) * 1.25f;
            if (Mathf.Sign(physics.floorAngle) != Mathf.Sign(body.velocity.x))
                boost = 0;

            body.velocity = new(body.velocity.x, bounceHeight + boost);
        } else if (isIceball && body.velocity.y > 0.1f)  {
            BreakOnImpact = true;
        }
        bool breaking = physics.hitLeft || physics.hitRight || physics.hitRoof || (physics.onGround && BreakOnImpact);
        if (breaking) {
            Runner.Despawn(Object);
            return false;
        }

        return true;
    }

    private readonly Collider2D[] collisions = new Collider2D[8];
    public bool CheckForEntityCollision() {

        int count = Runner.GetPhysicsScene2D().OverlapBox(body.position + physics.currentCollider.offset, ((BoxCollider2D) physics.currentCollider).size, 0, default, collisions);

        for (int i = 0; i < count; i++) {
            GameObject collidedObject = collisions[i].gameObject;

            //don't interact with ourselves.
            if (collisions[i].attachedRigidbody == body)
                continue;

            if (collidedObject.GetComponentInParent<IFireballInteractable>() is IFireballInteractable interactable) {
                bool result = isIceball ? interactable.InteractWithIceball(this) : interactable.InteractWithFireball(this);
                if (result) {
                    //true = interacted & despawn.
                    Runner.Despawn(Object);
                    return false;
                }
            }
        }

        return true;
    }

    public override void Despawned(NetworkRunner runner, bool hasState) {
        if (!GameManager.Instance.gameover)
            Instantiate(wallHitPrefab, transform.position, Quaternion.identity);

        particles.transform.parent = null;
        particles.Stop();
    }

    public void InteractWithPlayer(PlayerController player) {
        //Check if they own us. If so, don't collide.
        if (Object.InputAuthority == player.Object.InputAuthority)
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
            if (isIceball && (player.IsInShell || player.IsCrouching || player.IsGroundpounding))
                player.ShellSlowdownTimer = TickTimer.CreateFromSeconds(Runner, 0.65f);
            return;
        }
        }

        //Collision is a GO
        if (isIceball) {
            //iceball
            //TODO:
            player.Freeze(null);
        } else {
            //fireball
            //TODO: damage source?
            player.DoKnockback(FacingRight, 1, true, 0);
        }

        //Destroy ourselves.
        Runner.Despawn(Object);
    }

    public bool InteractWithFireball(FireballMover fireball) {
        //fire + ice = both destroy
        if (isIceball) {
            Runner.Despawn(fireball.Object);
            return true;
        }
        return false;
    }

    public bool InteractWithIceball(FireballMover iceball) {
        //fire + ice = both destroy
        if (!isIceball) {
            Runner.Despawn(iceball.Object);
            return true;
        }
        return false;
    }

    public override void Bump(BasicEntity bumper, Vector3Int tile, InteractableTile.InteractionDirection direction) {
        //do nothing when bumped
    }
}
