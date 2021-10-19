using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class MovingPowerup : MonoBehaviourPun {

    private static int groundMask = -1;
    [SerializeField] float speed, bouncePower, terminalVelocity = 4, blinkingRate = 4;
    Rigidbody2D body;
    BoxCollider2D boxCollider;
    new SpriteRenderer renderer;
    bool right = true;
    public bool passthrough = false;
    public GameObject followMe;
    public float followMeCounter;
    private PhysicsEntity physics;

    void Start() {
        body = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();
        renderer = GetComponent<SpriteRenderer>();
        physics = GetComponent<PhysicsEntity>();

        if (groundMask == -1)
            groundMask = LayerMask.GetMask("Ground", "PassthroughInvalid");
    }

    [PunRPC]
    void SetFollowMe(int view) {
        PhotonView followView = PhotonView.Find(view);
        photonView.TransferOwnership(followView.Owner);
        followMe = followView.gameObject;
        followMeCounter = 2f;
        passthrough = true;
    }

    void FixedUpdate() {
        if (GameManager.Instance && GameManager.Instance.gameover) {
            body.velocity = Vector2.zero;
            body.isKinematic = true;
            return;
        }

        if (followMe) {
            body.isKinematic = true;
            if (photonView.IsMine) {
                transform.position = new Vector3(followMe.transform.position.x, Camera.main.transform.position.y + ((float) Camera.main.GetComponent<HorizontalCamera>().m_orthographicSize - Camera.main.orthographicSize) + 1.5f);
            }

            if ((followMeCounter * blinkingRate) % 2 < 1) {
                renderer.enabled = false;
            } else {
                renderer.enabled = true;
            }
            if ((followMeCounter -= Time.fixedDeltaTime) < 0) {
                followMe = null;
                if (photonView.IsMine) {
                    photonView.TransferOwnership(PhotonNetwork.MasterClient);
                    passthrough = true;
                }
            }
            gameObject.layer = LayerMask.NameToLayer("HitsNothing");
        } else {
            renderer.enabled = true;
            renderer.color = Color.white;
            body.isKinematic = false;
            if (passthrough) {
                if (!Physics2D.OverlapBox(transform.position, Vector2.one / 3f, 0, groundMask)) {
                    gameObject.layer = LayerMask.NameToLayer("Entity");
                    passthrough = false;
                }
            }
        }
        HandleCollision();

        body.velocity = new Vector2(body.velocity.x, Mathf.Max(-terminalVelocity, body.velocity.y));
    }
    void HandleCollision() {
        physics.Update();

        if (physics.hitLeft || physics.hitRight) {
            if (physics.hitLeft) {
                right = true;
            }
            if (physics.hitRight) {
                right = false;
            }
            body.velocity = new Vector2(speed * (right ? 1 : -1), body.velocity.y);
        }
        if (physics.onGround) {
            body.velocity = new Vector2(speed * (right ? 1 : -1), bouncePower);
            if (physics.hitRoof) {
                photonView.RPC("Crushed", RpcTarget.All);
                return;
            }
        }
    }

    [PunRPC]
    public void Crushed() {
        if (photonView.IsMine)
            PhotonNetwork.Destroy(gameObject);
        GameObject.Instantiate(Resources.Load("Prefabs/Particle/Puff"), transform.position, Quaternion.identity);
    }
}
