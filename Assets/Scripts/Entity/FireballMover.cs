using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class FireballMover : MonoBehaviourPun {
    public float speed = 3f, bounceHeight = 4.5f;
    public bool left;
    public bool isIceball;
    private Rigidbody2D body;
    private PhysicsEntity physics;
    bool breakOnImpact;

    void Start() {
        body = GetComponent<Rigidbody2D>();
        physics = GetComponent<PhysicsEntity>();

        left = (bool) photonView.InstantiationData[0];
        body.velocity = new Vector2(speed * (left ? -1 : 1), -speed);
    }
    void FixedUpdate() {
        if (GameManager.Instance && GameManager.Instance.gameover) {
            body.velocity = Vector2.zero;
            GetComponent<Animator>().enabled = false;
            body.isKinematic = true;
            return;
        }

        HandleCollision();

        float gravityInOneFrame = body.gravityScale * Physics2D.gravity.y * Time.fixedDeltaTime;
        body.velocity = new Vector2(speed * (left ? -1 : 1), Mathf.Max(-speed - gravityInOneFrame, body.velocity.y));
    }
    void HandleCollision() {
        physics.UpdateCollisions();

        if (physics.onGround) {
            float boost = bounceHeight * Mathf.Sin(physics.floorAngle * Mathf.Deg2Rad) * 1.5f;
            if (Mathf.Sign(physics.floorAngle) != Mathf.Sign(body.velocity.x))
                boost = 0;
            
            body.velocity = new Vector2(body.velocity.x, bounceHeight + boost);
        } else if (isIceball && body.velocity.y > 1.5f)  {
            breakOnImpact = true;
        }
        bool breaking = physics.hitLeft || physics.hitRight || physics.hitRoof || (physics.onGround && breakOnImpact);
        if (photonView && photonView.IsMine && breaking)
            PhotonNetwork.Destroy(gameObject);
    }

    void OnDestroy() {
        Instantiate(Resources.Load("Prefabs/Particle/" + (isIceball ? "IceballWall" : "FireballWall")), transform.position, Quaternion.identity);
    }

    [PunRPC]
    protected void Kill() {
        if (photonView.IsMine)
            PhotonNetwork.Destroy(photonView);
    }

    void OnTriggerEnter2D(Collider2D collider) {
        if (!photonView.IsMine)
            return;

        switch (collider.tag) {
        case "koopa":
        case "goomba": {
            KillableEntity en = collider.gameObject.GetComponentInParent<KillableEntity>();
            if (en.dead || en.frozen)
                return;
            if (isIceball) {
                GameObject frozenBlock = PhotonNetwork.Instantiate("Prefabs/FrozenCube", en.transform.position + new Vector3(0, 0.1f, 0), Quaternion.identity);
                frozenBlock.GetComponent<FrozenCube>().photonView.RPC("setFrozenEntity", RpcTarget.All, en.gameObject.tag, en.photonView.ViewID);
                PhotonNetwork.Destroy(gameObject);
            } else {
                en.photonView.RPC("SpecialKill", RpcTarget.All, !left, false);
                PhotonNetwork.Destroy(gameObject);
            }
            break;
        }
        case "frozencube": {
            FrozenCube fc = collider.gameObject.GetComponentInParent<FrozenCube>();
            if (fc.dead)
                return;
            // TODO: Stuff here

            if (isIceball) {
                PhotonNetwork.Destroy(gameObject);
            } else {
                fc.gameObject.GetComponent<FrozenCube>().photonView.RPC("Kill", RpcTarget.All);
                PhotonNetwork.Destroy(gameObject);
            }
            break;
        }
        case "Fireball": {
            if (isIceball) {
                PhotonNetwork.Destroy(collider.gameObject);
                PhotonNetwork.Destroy(gameObject);
            }
            break;
        }
        case "bulletbill": {
            KillableEntity bb = collider.gameObject.GetComponentInParent<BulletBillMover>();
            if (isIceball && !bb.frozen) {
                GameObject frozenBlock = PhotonNetwork.Instantiate("Prefabs/FrozenCube", bb.transform.position + new Vector3(0, 0.1f, 0), Quaternion.identity);
                frozenBlock.GetComponent<FrozenCube>().photonView.RPC("setFrozenEntity", RpcTarget.All, bb.gameObject.tag, bb.photonView.ViewID);
            }
            PhotonNetwork.Destroy(gameObject);

            break;
        }
        case "bobomb": {
            BobombWalk bobomb = collider.gameObject.GetComponentInParent<BobombWalk>();
            if (bobomb.dead || bobomb.frozen)
                return;
            if (!isIceball) {
                if (!bobomb.lit) {
                    bobomb.photonView.RPC("Light", RpcTarget.All);
                } else {
                    bobomb.photonView.RPC("Kick", RpcTarget.All, body.position.x < bobomb.body.position.x, false);
                }
                PhotonNetwork.Destroy(gameObject);
            } else {
                GameObject frozenBlock = PhotonNetwork.Instantiate("Prefabs/FrozenCube", bobomb.transform.position + new Vector3(0, 0.1f, 0), Quaternion.identity);
                frozenBlock.GetComponent<FrozenCube>().photonView.RPC("setFrozenEntity", RpcTarget.All, bobomb.gameObject.tag, bobomb.photonView.ViewID);
                PhotonNetwork.Destroy(gameObject);
            }
            break;
        }
        case "piranhaplant": {
            KillableEntity killa = collider.gameObject.GetComponentInParent<KillableEntity>();
            if (killa.dead)
                return;
            AnimatorStateInfo asi = killa.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0);
            if (asi.IsName("end") && asi.normalizedTime > 0.5f)
                return;
            if (!isIceball) {
                killa.photonView.RPC("Kill", RpcTarget.All);
                PhotonNetwork.Destroy(gameObject);
            } else {
                GameObject frozenBlock = PhotonNetwork.Instantiate("Prefabs/FrozenCube", killa.transform.position + new Vector3(0, 0.1f, 0), Quaternion.identity);
                frozenBlock.GetComponent<FrozenCube>().photonView.RPC("setFrozenEntity", RpcTarget.All, killa.gameObject.tag, killa.photonView.ViewID);
            }
            break;
        }
        }
    }
}
