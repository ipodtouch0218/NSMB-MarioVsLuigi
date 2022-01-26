using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class MovingPowerup : MonoBehaviourPun {

    private static int groundMask = -1;
    [SerializeField] float speed, bouncePower, terminalVelocity = 4, blinkingRate = 4;
    private Rigidbody2D body;
    private BoxCollider2D hitbox;
    private SpriteRenderer sRenderer;
    private bool right = true;
    public bool passthrough, avoidPlayers;
    public PlayerController followMe;
    public float followMeCounter, despawnCounter = 15, ignoreCounter;
    private PhysicsEntity physics;
    void Start() {
        body = GetComponent<Rigidbody2D>();
        hitbox = GetComponent<BoxCollider2D>();
        sRenderer = GetComponent<SpriteRenderer>();
        physics = GetComponent<PhysicsEntity>();

        object[] data = photonView.InstantiationData;
        if (data != null) {
            if (data[0] is float) {
                ignoreCounter = (float) data[0];
            } else if (data[0] is int) {
                followMe = PhotonView.Find((int) data[0]).GetComponent<PlayerController>();
                followMeCounter = 1.5f;
                passthrough = true;
                body.isKinematic = true;
            }
        }

        if (groundMask == -1)
            groundMask = LayerMask.GetMask("Ground", "PassthroughInvalid");
    }
    void LateUpdate() {
        ignoreCounter -= Time.deltaTime;
        if (!followMe) return;

        float size = (followMe.flying ? 3.8f : 2.8f);
        transform.position = new Vector3(followMe.transform.position.x, followMe.cameraController.currentPosition.y + (size*0.6f));

        sRenderer.enabled = (followMeCounter * blinkingRate) % 2 > 1;
        if ((followMeCounter -= Time.deltaTime) < 0) {
            followMe = null;
            if (photonView.IsMine) {
                photonView.TransferOwnership(PhotonNetwork.MasterClient);
                passthrough = true;
            }
        }
        gameObject.layer = LayerMask.NameToLayer("HitsNothing");
    }

    void FixedUpdate() {
        if (GameManager.Instance && GameManager.Instance.gameover) {
            body.velocity = Vector2.zero;
            body.isKinematic = true;
            return;
        }

        if (followMe) return;

        despawnCounter -= Time.fixedDeltaTime;
        if (despawnCounter <= 3) {
            if ((despawnCounter * blinkingRate) % 1 < 0.5f) {
                sRenderer.enabled = false;
            } else {
                sRenderer.enabled = true;
            }
        } else {
            sRenderer.enabled = true;
        }
        if (despawnCounter <= 0 && photonView.IsMine) {
            PhotonNetwork.Destroy(photonView);
        }

        sRenderer.color = Color.white;
        body.isKinematic = false;
        if (passthrough) {
            if (!Utils.IsTileSolidAtWorldLocation(body.position) && !Physics2D.OverlapBox(body.position, Vector2.one / 3f, 0, groundMask)) {
                gameObject.layer = LayerMask.NameToLayer("Entity");
                passthrough = false;
            }
        }
        HandleCollision();
        if (!followMe && !passthrough && avoidPlayers && physics.onGround) {
            Collider2D closest = null;
            float distance = float.MaxValue;
            foreach (var hit in Physics2D.OverlapCircleAll(body.position, 10f)) {
                if (hit.tag != "Player") continue;
                float tempDistance = Vector2.Distance(hit.attachedRigidbody.position, body.position);
                if (tempDistance > distance) continue;
                distance = tempDistance;    
                closest = hit;
            }
            Vector2 offset = new Vector2(GameManager.Instance.levelWidthTile/2f, 0);
            float centerOfMap = offset.x + GameManager.Instance.GetLevelMinX();
            bool offsets = false;
            if (body.position.x < centerOfMap) {
                offset = -offset;
            }
            foreach (var hit in Physics2D.OverlapCircleAll(body.position + offset, 10f)) {
                if (hit.tag != "Player") continue;
                float tempDistance = Vector2.Distance(hit.attachedRigidbody.position, body.position + offset);
                if (tempDistance > distance) continue;
                distance = tempDistance;
                closest = hit;
                offsets = true;
            }
            if (closest) {
                right = ((closest.attachedRigidbody.position.x - body.position.x) < 0) ^ offsets; 
            }
        }

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

    private void OnDrawGizmos() {
        Gizmos.DrawWireSphere(body.position, 10f);
        Vector2 offset = new Vector2(GameManager.Instance.levelWidthTile/2f, 0);
        float centerOfMap = offset.x + GameManager.Instance.GetLevelMinX();
        if (body.position.x < centerOfMap) {
            offset = -offset;
        }
        Gizmos.DrawWireSphere(body.position + offset, 10f);
    }
}
