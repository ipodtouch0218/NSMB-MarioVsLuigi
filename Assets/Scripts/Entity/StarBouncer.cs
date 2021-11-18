using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class StarBouncer : MonoBehaviourPun {

    private static int groundMask = -1;
    public bool stationary = true;
    [SerializeField] float pulseAmount = 0.2f, pulseSpeed = 0.2f, moveSpeed = 3f, rotationSpeed = 30f, bounceAmount = 4f, deathBoostAmount = 20f, blinkingSpeed = 0.5f, lifespan = 15f;
    float counter;
    Vector3 startingScale;
    private Rigidbody2D body;
    new private BoxCollider2D collider;
    public bool passthrough = true, left = true;
    private PhysicsEntity physics;

    void Start() {
        startingScale = transform.localScale;
        body = GetComponent<Rigidbody2D>();
        collider = GetComponent<BoxCollider2D>();
        physics = GetComponent<PhysicsEntity>();

        if (groundMask == -1)
            groundMask = LayerMask.GetMask("Ground", "PassthroughInvalid");
        
        object[] data = photonView.InstantiationData;
        if (data != null && data.Length >= 1) {
            stationary = false;
            passthrough = true;
            gameObject.layer = LayerMask.NameToLayer("HitsNothing");
            left = (bool) data[0];
        }

        GameObject trackObject = GameObject.Instantiate(UIUpdater.instance.starTrackTemplate, UIUpdater.instance.starTrackTemplate.transform.position, Quaternion.identity, UIUpdater.instance.transform);
        TrackIcon icon = trackObject.GetComponent<TrackIcon>();
        icon.target = gameObject;
        if (!stationary) {
            trackObject.transform.localScale = new Vector3(3f/4f, 3f/4f, 1f);
            body.velocity = new Vector2(moveSpeed * (left ? -1 : 1), deathBoostAmount);
        }
        trackObject.SetActive(true);
    }

    void FixedUpdate() {
        if (GameManager.Instance && GameManager.Instance.gameover) {
            body.velocity = Vector2.zero;
            body.isKinematic = true;
            return;
        }

        if (stationary) {
            counter += Time.fixedDeltaTime;
            float sin = (Mathf.Sin(counter * pulseSpeed)) * pulseAmount;
            transform.localScale = startingScale + new Vector3(sin, sin, 0);
            return;
        } else {
            body.velocity = new Vector2(moveSpeed * (left ? -1 : 1), body.velocity.y);
        }

        HandleCollision();

        lifespan -= Time.fixedDeltaTime;

        if (lifespan < 5f) {
            if ((lifespan * 2f) % (blinkingSpeed*2) < blinkingSpeed) {
                GetComponentInChildren<SpriteRenderer>().color = new Color(0,0,0,0);
            } else {
                GetComponentInChildren<SpriteRenderer>().color = Color.white;
            }
        }
        
        // bool left = body.velocity.x < 0;
        Transform t = transform.Find("Graphic");
        t.Rotate(new Vector3(0, 0, rotationSpeed * (left ? 1 : -1)), Space.Self);

        if (passthrough) {
            // gameObject.layer = LayerMask.NameToLayer("HitsNothing");
            if (body.velocity.y <= 0 && !Physics2D.OverlapBox(transform.position, Vector2.one / 3, 0, groundMask)) {
                passthrough = false;
                gameObject.layer = LayerMask.NameToLayer("Entity");
            }
        }

        if (!photonView.IsMine || stationary) {
            body.isKinematic = true;
            return;
        } else {
            body.isKinematic = false;
            transform.localScale = startingScale;
        }

        if (lifespan < 0 || (!passthrough && transform.position.y < GameManager.Instance.GetLevelMinY())) {
            photonView.RPC("Crushed", RpcTarget.All);
        }
    }

    void HandleCollision() {
        physics.Update();

        if (physics.hitLeft) {
            photonView.RPC("Turnaround", RpcTarget.All, true);
        }
        if (physics.hitRight) {
            photonView.RPC("Turnaround", RpcTarget.All, false);
        }
        if (physics.onGround && physics.hitRoof) {
            photonView.RPC("Crushed", RpcTarget.All);
            return;
        }
        if (physics.onGround) {
            body.velocity = new Vector2(body.velocity.x, bounceAmount);
        }
    }

    [PunRPC]
    public void Crushed() {
        if (photonView.IsMine)
            PhotonNetwork.Destroy(gameObject);
        GameObject.Instantiate(Resources.Load("Prefabs/Particle/Puff"), transform.position, Quaternion.identity);
    }
    [PunRPC]
    public void Turnaround(bool hitLeft) {
        left = !hitLeft;
        body.velocity = new Vector2(moveSpeed * (left ? -1 : 1), body.velocity.y);
    }
}
