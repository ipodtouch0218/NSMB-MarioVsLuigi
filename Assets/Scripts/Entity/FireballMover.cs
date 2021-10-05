using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class FireballMover : MonoBehaviourPun {
    public float speed = 3f;
    public bool left;
    public int owner;
    private Rigidbody2D body;
    private PhysicsEntity physics;

    void Start() {
        body = GetComponent<Rigidbody2D>();
        physics = GetComponent<PhysicsEntity>();
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

    [PunRPC]
    void Instantiate(int ownerView, bool left) {
        this.left = left;
        body = GetComponent<Rigidbody2D>();
        body.velocity = new Vector2(speed * (left ? -1 : 1), -speed);
        owner = ownerView;
    }

    void HandleCollision() {
        physics.Update();

        if (photonView && photonView.IsMine && (physics.hitLeft || physics.hitRight || physics.hitRoof)) {
            photonView.RPC("Kill", RpcTarget.All);
        }

        if (physics.onGround) {
            body.velocity = new Vector2(body.velocity.x, speed/1.25f);
            if (physics.hitRoof) {
                photonView.RPC("Kill", RpcTarget.All);
                return;
            }
        }
    }

    [PunRPC]
    public void Kill() {
        if (photonView.IsMine)
            PhotonNetwork.Destroy(gameObject);
        GameObject.Instantiate(Resources.Load("FireballWallParticle"), transform.position, Quaternion.identity);
    }

    void OnTriggerEnter2D(Collider2D collider) {
        if (!photonView.IsMine) {
            return;
        }
        switch (collider.tag) {
            case "koopa":
            case "goomba": {
                KillableEntity en = collider.gameObject.GetComponentInParent<KillableEntity>();
                if (en.dead) return;
                en.photonView.RPC("SpecialKill", RpcTarget.All, !left, false);
                PhotonNetwork.Destroy(gameObject);
                break;
            }
            case "bobomb": {
                BobombWalk bobomb = collider.gameObject.GetComponentInParent<BobombWalk>();
                if (bobomb.dead) return;
                if (!bobomb.lit) {
                    bobomb.photonView.RPC("Light", RpcTarget.All);   
                } else {
                    bobomb.photonView.RPC("Kick", RpcTarget.All, transform.position.x < bobomb.transform.position.x);
                }
                PhotonNetwork.Destroy(gameObject);
                break;
            }
            case "piranhaplant": {
                KillableEntity killa = collider.gameObject.GetComponentInParent<KillableEntity>();
                if (killa.dead) return;
                killa.photonView.RPC("Kill", RpcTarget.All);
                PhotonNetwork.Destroy(gameObject);
                break;
            }
        }
    }
}
