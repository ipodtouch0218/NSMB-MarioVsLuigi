using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class FireballMover : MonoBehaviourPun {
    public float speed = 3f;
    public bool left;
    public bool isIceball;
    private Rigidbody2D body;
    private PhysicsEntity physics;
    

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

        body.velocity = new Vector2(speed * (left ? -1 : 1), Mathf.Max(-speed, body.velocity.y));
    }
    void HandleCollision() {
        physics.UpdateCollisions();

        if (physics.onGround)
            body.velocity = new Vector2(body.velocity.x, speed / 1.25f);

        if (photonView && photonView.IsMine && (physics.hitLeft || physics.hitRight || physics.hitRoof))
            PhotonNetwork.Destroy(gameObject);
    }

    void OnDestroy() {
        if (isIceball) {
            Instantiate(Resources.Load("Prefabs/Particle/IceballWall"), transform.position, Quaternion.identity);
        } else {
            Instantiate(Resources.Load("Prefabs/Particle/FireballWall"), transform.position, Quaternion.identity);
        }

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
                if (en.dead) 
                    return;
                if (isIceball && !en.frozen) {
                    GameObject frozenBlock = PhotonNetwork.Instantiate("Prefabs/FrozenCube", en.transform.position + new Vector3(0, 0.1f, 0), Quaternion.identity);
                    frozenBlock.gameObject.GetComponent<FrozenCube>().photonView.RPC("setFrozenEntity", RpcTarget.All, en.gameObject.tag, en.photonView.ViewID);
                    //PhotonNetwork.Destroy(en.gameObject);
                    // TODO: give enemy bool left value to FrozenCube so when it melts it spawns the enemy back facing the right way
                    PhotonNetwork.Destroy(gameObject);
                } else if (!isIceball && !en.frozen) {
                    en.photonView.RPC("SpecialKill", RpcTarget.All, !left, false);
                    PhotonNetwork.Destroy(gameObject);
                }
                break;
            }
            case "FrozenCube": {

                // TODO: Stuff here

                break;
            }
            case "bobomb": {
                BobombWalk bobomb = collider.gameObject.GetComponentInParent<BobombWalk>();
                if (bobomb.dead) 
                    return;
                if (!bobomb.lit) {
                    bobomb.photonView.RPC("Light", RpcTarget.All);   
                } else {
                    bobomb.photonView.RPC("Kick", RpcTarget.All, body.position.x < bobomb.body.position.x, false);
                }
                PhotonNetwork.Destroy(gameObject);
                break;
            }
            case "piranhaplant": {
                KillableEntity killa = collider.gameObject.GetComponentInParent<KillableEntity>();
                if (killa.dead) 
                    return;
                AnimatorStateInfo asi = killa.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0);
                if (asi.IsName("end") && asi.normalizedTime > 0.5f) 
                    return;
                killa.photonView.RPC("Kill", RpcTarget.All);
                PhotonNetwork.Destroy(gameObject);
                break;
            }
        }
    }
}
