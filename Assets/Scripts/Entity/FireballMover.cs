using UnityEngine;

using Fusion;

public class FireballMover : BasicEntity, IPlayerInteractable {

    //---Networked Variables
    [Networked] public NetworkBool IsIceball { get; set; }
    [Networked] public NetworkBool BreakOnImpact { get; set; }

    //---Public Variables
    public PlayerController owner;
    public GameObject wallHitPrefab;

    //---Serialized Variables
    [SerializeField] private float speed = 3f, bounceHeight = 4.5f, terminalVelocity = 6.25f;

    //---Private Variables
    private PhysicsEntity physics;

    public void OnBeforeSpawned(PlayerController owner, bool right) {
        this.owner = owner;
        FacingRight = right;

        if (IsIceball)
            speed += Mathf.Abs(owner.body.velocity.x / 3f);
    }

    public override void Awake() {
        base.Awake();
        physics = GetComponent<PhysicsEntity>();
    }

    public override void Spawned() {
        base.Spawned();

        body.velocity = new(speed * (FacingRight ? 1 : -1), -speed);
    }

    public override void FixedUpdateNetwork() {
        if (GameManager.Instance && GameManager.Instance.gameover) {
            body.velocity = Vector2.zero;
            GetComponent<Animator>().enabled = false;
            body.isKinematic = true;
            return;
        }

        HandleCollision();

        body.velocity = new(speed * (FacingRight ? 1 : -1), Mathf.Max(-terminalVelocity, body.velocity.y));
    }

    private void HandleCollision() {
        physics.UpdateCollisions();

        if (physics.onGround && !BreakOnImpact) {
            float boost = bounceHeight * Mathf.Abs(Mathf.Sin(physics.floorAngle * Mathf.Deg2Rad)) * 1.25f;
            if (Mathf.Sign(physics.floorAngle) != Mathf.Sign(body.velocity.x))
                boost = 0;

            body.velocity = new Vector2(body.velocity.x, bounceHeight + boost);
        } else if (IsIceball && body.velocity.y > 1.5f)  {
            BreakOnImpact = true;
        }
        bool breaking = physics.hitLeft || physics.hitRight || physics.hitRoof || (physics.onGround && BreakOnImpact);
        if (breaking) {
            Runner.Despawn(Object);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState) {
        if (!GameManager.Instance.gameover)
            Instantiate(wallHitPrefab, transform.position, Quaternion.identity);
    }


    private void OnTriggerEnter2D(Collider2D collider) {
        switch (collider.tag) {
        case "koopa":
        case "goomba": {
            KillableEntity en = collider.gameObject.GetComponentInParent<KillableEntity>();
            if (en.Dead || en.IsFrozen)
                return;

            if (IsIceball) {
                //TODO:
                //PhotonNetwork.Instantiate("Prefabs/FrozenCube", en.transform.position + new Vector3(0, 0.1f, 0), Quaternion.identity, 0, new object[] { en.photonView.ViewID });
            } else {
                en.SpecialKill(FacingRight, false, 0);
            }
            Runner.Despawn(Object);
            break;
        }
        case "frozencube": {
            FrozenCube fc = collider.gameObject.GetComponent<FrozenCube>();
            if (fc.Dead)
                return;

            if (!IsIceball) {
                fc.Kill();
            }

            Runner.Despawn(Object);
            break;
        }
        case "Fireball": {
            FireballMover otherball = collider.gameObject.GetComponentInParent<FireballMover>();
            if (IsIceball ^ otherball.IsIceball) {
                Runner.Despawn(Object);
                Runner.Despawn(otherball.Object);
            }
            break;
        }
        case "bulletbill": {
            KillableEntity bb = collider.gameObject.GetComponentInParent<BulletBillMover>();
            if (IsIceball && !bb.IsFrozen) {
                //TODO:
                //PhotonNetwork.Instantiate("Prefabs/FrozenCube", bb.transform.position + new Vector3(0, 0.1f, 0), Quaternion.identity, 0, new object[] { bb.photonView.ViewID });
            }
            Runner.Despawn(Object);
            break;
        }
        case "bobomb": {
            BobombWalk bobomb = collider.gameObject.GetComponentInParent<BobombWalk>();
            if (bobomb.Dead || bobomb.IsFrozen)
                return;

            if (!IsIceball) {
                if (!bobomb.Lit) {
                    bobomb.Light();
                } else {
                    bobomb.Kick(body.position.x < bobomb.body.position.x, 0f, false);
                }
            } else {
                //TODO:
                //PhotonNetwork.Instantiate("Prefabs/FrozenCube", bobomb.transform.position + new Vector3(0, 0.1f, 0), Quaternion.identity, 0, new object[] { bobomb.photonView.ViewID });
            }

            Runner.Despawn(Object);
            break;
        }
        case "piranhaplant": {
            KillableEntity killa = collider.gameObject.GetComponentInParent<KillableEntity>();
            if (killa.Dead)
                return;
            AnimatorStateInfo asi = killa.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0);
            if (asi.IsName("end") && asi.normalizedTime > 0.5f)
                return;

            if (!IsIceball) {
                killa.Kill();
            } else {
                //TODO:
                //PhotonNetwork.Instantiate("Prefabs/FrozenCube", killa.transform.position + new Vector3(0, 0.1f, 0), Quaternion.identity, 0, new object[] { killa.photonView.ViewID });
            }
            Runner.Despawn(Object);
            break;
        }
        }
    }

    public void InteractWithPlayer(PlayerController player) {
        //Check if they own us. If so, don't collide.
        if (owner == player.Object.InputAuthority)
            return;

        //If they have knockback invincibility, don't collide.
        if (!player.DamageInvincibilityTimer.ExpiredOrNotRunning(Runner))
            return;

        //Destroy ourselves.
        Runner.Despawn(Object);

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
            if (IsIceball && (player.inShell || player.crouching || player.groundpound))
                player.ShellSlowdownTimer = TickTimer.CreateFromSeconds(Runner, 0.65f);
            return;
        }
        }

        //Collision is a GO
        if (IsIceball) {
            //iceball
            //TODO:
            player.Freeze(null);
        } else {
            //fireball
            //TODO: damage source?
            player.DoKnockback(FacingRight, 1, true, 0);
        }
    }

    public override void Bump(BasicEntity bumper, Vector3Int tile, InteractableTile.InteractionDirection direction) {
        //do nothing when bumped
    }
}
