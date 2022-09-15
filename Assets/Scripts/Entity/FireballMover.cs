using UnityEngine;

using Fusion;

public class FireballMover : NetworkBehaviour {

    //---Networked Variables
    [Networked] public NetworkBool FacingRight { get; set; }
    [Networked] public NetworkBool IsIceball { get; set; }

    //---Public Variables
    public GameObject wallHitPrefab;
    public PlayerRef owner;

    //---Serialized Variables
    [SerializeField] private float speed = 3f, bounceHeight = 4.5f, terminalVelocity = 6.25f;

    //---Private Variables
    private Rigidbody2D body;
    private PhysicsEntity physics;
    private bool breakOnImpact;

    public void Awake() {
        body = GetComponent<Rigidbody2D>();
        physics = GetComponent<PhysicsEntity>();
    }

    public void OnBeforeSpawned(PlayerRef owner, bool right, float shooterSpeed) {
        this.owner = owner;
        FacingRight = right;

        if (IsIceball)
            speed += Mathf.Abs(shooterSpeed / 3f);
    }

    public void Start() {
        body.velocity = new Vector2(speed * (FacingRight ? 1 : -1), -speed);
    }

    public override void FixedUpdateNetwork() {
        if (GameManager.Instance && GameManager.Instance.gameover) {
            body.velocity = Vector2.zero;
            GetComponent<Animator>().enabled = false;
            body.isKinematic = true;
            return;
        }

        HandleCollision();

        float gravityInOneFrame = body.gravityScale * Physics2D.gravity.y * Time.fixedDeltaTime;
        body.velocity = new Vector2(speed * (FacingRight ? 1 : -1), Mathf.Max(-terminalVelocity, body.velocity.y));
    }

    private void HandleCollision() {
        physics.UpdateCollisions();

        if (physics.onGround && !breakOnImpact) {
            float boost = bounceHeight * Mathf.Abs(Mathf.Sin(physics.floorAngle * Mathf.Deg2Rad)) * 1.25f;
            if (Mathf.Sign(physics.floorAngle) != Mathf.Sign(body.velocity.x))
                boost = 0;

            body.velocity = new Vector2(body.velocity.x, bounceHeight + boost);
        } else if (IsIceball && body.velocity.y > 1.5f)  {
            breakOnImpact = true;
        }
        bool breaking = physics.hitLeft || physics.hitRight || physics.hitRoof || (physics.onGround && breakOnImpact);
        if (breaking) {
            Runner.Despawn(Object, true);

        }
    }

    public void OnDestroy() {
        if (!GameManager.Instance.gameover)
            Instantiate(wallHitPrefab, transform.position, Quaternion.identity);
    }


    private void OnTriggerEnter2D(Collider2D collider) {
        switch (collider.tag) {
        case "koopa":
        case "goomba": {
            KillableEntity en = collider.gameObject.GetComponentInParent<KillableEntity>();
            if (en.dead || en.Frozen)
                return;

            if (IsIceball) {
                //TODO:
                //PhotonNetwork.Instantiate("Prefabs/FrozenCube", en.transform.position + new Vector3(0, 0.1f, 0), Quaternion.identity, 0, new object[] { en.photonView.ViewID });
            } else {
                en.SpecialKill(FacingRight, false, 0);
            }
            Runner.Despawn(Object, true);
            break;
        }
        case "frozencube": {
            FrozenCube fc = collider.gameObject.GetComponent<FrozenCube>();
            if (fc.dead)
                return;

            if (!IsIceball) {
                fc.Kill();
            }

            Runner.Despawn(Object, true);
            break;
        }
        case "Fireball": {
            FireballMover otherball = collider.gameObject.GetComponentInParent<FireballMover>();
            if (IsIceball ^ otherball.IsIceball) {
                Runner.Despawn(Object, true);
                Runner.Despawn(otherball.Object, true);
            }
            break;
        }
        case "bulletbill": {
            KillableEntity bb = collider.gameObject.GetComponentInParent<BulletBillMover>();
            if (IsIceball && !bb.Frozen) {
                //TODO:
                //PhotonNetwork.Instantiate("Prefabs/FrozenCube", bb.transform.position + new Vector3(0, 0.1f, 0), Quaternion.identity, 0, new object[] { bb.photonView.ViewID });
            }
            Runner.Despawn(Object, true);
            break;
        }
        case "bobomb": {
            BobombWalk bobomb = collider.gameObject.GetComponentInParent<BobombWalk>();
            if (bobomb.dead || bobomb.Frozen)
                return;

            if (!IsIceball) {
                if (!bobomb.lit) {
                    bobomb.Light();
                } else {
                    bobomb.Kick(body.position.x < bobomb.body.position.x, 0f, false);
                }
            } else {
                //TODO:
                //PhotonNetwork.Instantiate("Prefabs/FrozenCube", bobomb.transform.position + new Vector3(0, 0.1f, 0), Quaternion.identity, 0, new object[] { bobomb.photonView.ViewID });
            }

            Runner.Despawn(Object, true);
            break;
        }
        case "piranhaplant": {
            KillableEntity killa = collider.gameObject.GetComponentInParent<KillableEntity>();
            if (killa.dead)
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
            Runner.Despawn(Object, true);
            break;
        }
        }
    }
}
