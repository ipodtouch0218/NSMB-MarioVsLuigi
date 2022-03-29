using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class StarBouncer : MonoBehaviourPun {

    private static int ANY_GROUND_MASK = -1;
    public bool stationary = true;
    [SerializeField] float pulseAmount = 0.2f, pulseSpeed = 0.2f, moveSpeed = 3f, rotationSpeed = 30f, bounceAmount = 4f, deathBoostAmount = 20f, blinkingSpeed = 0.5f, lifespan = 15f, sparkleSoundDistance = 4f;
    public float counter, readyForUnPassthrough = 0.5f;
    private Vector3 startingScale;
    public Rigidbody2D body;
    private AudioSource sfx;
    private SpriteRenderer sRenderer;
    public bool passthrough = true, left = true;
    private PhysicsEntity physics;
    public int creator = -1;

    void Start() {
        startingScale = transform.localScale;
        body = GetComponent<Rigidbody2D>();
        physics = GetComponent<PhysicsEntity>();
        sRenderer = GetComponentInChildren<SpriteRenderer>();
        sfx = GetComponent<AudioSource>();

        object[] data = photonView.InstantiationData;
        if (data != null) {
            stationary = false;
            passthrough = true;
            gameObject.layer = LayerMask.NameToLayer("HitsNothing");
            left = (bool) data[0];
            creator = (int) data[1];
        }

        GameObject trackObject = Instantiate(UIUpdater.Instance.starTrackTemplate, UIUpdater.Instance.starTrackTemplate.transform.position, Quaternion.identity, UIUpdater.Instance.transform);
        TrackIcon icon = trackObject.GetComponent<TrackIcon>();
        icon.target = gameObject;
        if (!stationary) {
            trackObject.transform.localScale = new Vector3(3f/4f, 3f/4f, 1f);
            body.velocity += new Vector2(moveSpeed * (left ? -1 : 1), deathBoostAmount);
        }
        trackObject.SetActive(true);

        if (ANY_GROUND_MASK == -1)
            ANY_GROUND_MASK = LayerMask.GetMask("Ground", "PassthroughInvalid");
    }

    void Update() {
        if (GameManager.Instance.localPlayer == null)
            return;

        Vector3 loc = GameManager.Instance.localPlayer.transform.position;
        if (Mathf.Abs(loc.x - transform.position.x) > GameManager.Instance.levelWidthTile/4f) {
            loc.x -= GameManager.Instance.levelWidthTile / 2f * Mathf.Sign(loc.x - transform.position.x);
        }
        sfx.volume = Utils.QuadraticEaseOut(1 - Mathf.Clamp01(Vector2.Distance(loc, transform.position - (Vector3.down/2f)) / sparkleSoundDistance));
    }

    void FixedUpdate() {
        if (GameManager.Instance && GameManager.Instance.gameover) {
            body.velocity = Vector2.zero;
            body.isKinematic = true;
            return;
        }

        if (stationary) {
            counter += Time.fixedDeltaTime;
            float sin = Mathf.Sin(counter * pulseSpeed) * pulseAmount;
            transform.localScale = startingScale + new Vector3(sin, sin, 0);
            readyForUnPassthrough = -1;
            return;
        } else {
            body.velocity = new Vector2(moveSpeed * (left ? -1 : 1), body.velocity.y);
        }

        HandleCollision();

        lifespan -= Time.fixedDeltaTime;
        sRenderer.enabled = !(lifespan < 5 && lifespan * 2 % (blinkingSpeed * 2) < blinkingSpeed);
        
        Transform t = transform.Find("Graphic");
        t.Rotate(new Vector3(0, 0, rotationSpeed * (left ? 1 : -1)), Space.Self);

        if (passthrough && (readyForUnPassthrough -= Time.fixedDeltaTime) < 0 && body.velocity.y <= 0 && !Utils.IsTileSolidAtWorldLocation(body.position) && !Physics2D.OverlapBox(body.position, Vector2.one / 3, 0, ANY_GROUND_MASK)) {
            passthrough = false;
            gameObject.layer = LayerMask.NameToLayer("Entity");
        }

        if (!photonView.IsMine || stationary) {
            body.isKinematic = true;
            return;
        } else {
            body.isKinematic = false;
            transform.localScale = startingScale;
        }

        if (lifespan < 0 || (!passthrough && transform.position.y < GameManager.Instance.GetLevelMinY()))
            photonView.RPC("Crushed", RpcTarget.All);
    }

    void HandleCollision() {
        physics.UpdateCollisions();

        if (physics.hitLeft || physics.hitRight)
            photonView.RPC("Turnaround", RpcTarget.All, physics.hitLeft);
        if (physics.onGround) {
            body.velocity = new Vector2(body.velocity.x, bounceAmount);
            if (physics.hitRoof)
                photonView.RPC("Crushed", RpcTarget.All);
        }
    }

    [PunRPC]
    public void Crushed() {
        if (photonView.IsMine)
            PhotonNetwork.Destroy(gameObject);
        
        Instantiate(Resources.Load("Prefabs/Particle/Puff"), transform.position, Quaternion.identity);
    }
    [PunRPC]
    public void Turnaround(bool hitLeft) {
        left = !hitLeft;
        body.velocity = new Vector2(moveSpeed * (left ? -1 : 1), body.velocity.y);
    }
}
