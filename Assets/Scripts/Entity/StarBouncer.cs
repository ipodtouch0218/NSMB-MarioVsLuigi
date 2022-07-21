﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using NSMB.Utils;

public class StarBouncer : MonoBehaviourPun {

    private static int ANY_GROUND_MASK = -1;
    public bool stationary = true;
    [SerializeField] float pulseAmount = 0.2f, pulseSpeed = 0.2f, moveSpeed = 3f, rotationSpeed = 30f, bounceAmount = 4f, deathBoostAmount = 20f, blinkingSpeed = 0.5f, lifespan = 15f;
    public float counter;
    public Rigidbody2D body;
    private SpriteRenderer sRenderer;
    private Transform graphicTransform;
    public bool passthrough = true, left = true, fast = false;
    private PhysicsEntity physics;
    public int creator = -1;

    private BoxCollider2D worldCollider;

    private bool canBounce;

    public bool Collectable { get; private set; }

    public void Start() {
        body = GetComponent<Rigidbody2D>();
        physics = GetComponent<PhysicsEntity>();
        sRenderer = GetComponentInChildren<SpriteRenderer>();
        worldCollider = GetComponent<BoxCollider2D>();

        graphicTransform = transform.Find("Graphic");

        GameObject trackObject = Instantiate(UIUpdater.Instance.starTrackTemplate, UIUpdater.Instance.starTrackTemplate.transform.parent);
        TrackIcon icon = trackObject.GetComponent<TrackIcon>();
        icon.target = gameObject;
        trackObject.SetActive(true);

        object[] data = photonView.InstantiationData;
        if (data != null) {
            trackObject.transform.localScale = new(3f / 4f, 3f / 4f, 1f);
            stationary = false;
            passthrough = true;
            gameObject.layer = Layers.LayerHitsNothing;
            int direction = (int) data[0];
            left = direction <= 1;
            fast = direction == 0 || direction == 3;
            creator = (int) data[1];
            body.velocity = new(moveSpeed * (left ? -1 : 1), deathBoostAmount);

            //death via pit boost
            if ((bool) data[3])
                body.velocity += Vector2.up * 3;

            body.isKinematic = false;
            worldCollider.enabled = true;
        } else {
            GetComponent<Animator>().enabled = true;
            Collectable = true;
            body.isKinematic = true;
            body.velocity = Vector2.zero;
            GetComponent<CustomRigidbodySerializer>().enabled = false;

            if (GameManager.Instance.musicEnabled)
                GameManager.Instance.sfx.PlayOneShot(Enums.Sounds.World_Star_Spawn.GetClip());
        }

        if (ANY_GROUND_MASK == -1)
            ANY_GROUND_MASK = LayerMask.GetMask("Ground", "PassthroughInvalid");
    }

    public void Update() {
        if (GameManager.Instance && GameManager.Instance.gameover)
            return;

        if (stationary) {
            counter += Time.deltaTime;
            float sin = Mathf.Sin(counter * pulseSpeed) * pulseAmount;
            graphicTransform.localScale = Vector3.one * 3f + new Vector3(sin, sin, 0);
            return;
        }

        lifespan -= Time.deltaTime;
        sRenderer.enabled = !(lifespan < 5 && lifespan * 2 % (blinkingSpeed * 2) < blinkingSpeed);
        graphicTransform.Rotate(new(0, 0, rotationSpeed * 30 * (left ? 1 : -1) * Time.deltaTime), Space.Self);
    }

    public void FixedUpdate() {
        if (GameManager.Instance && GameManager.Instance.gameover) {
            body.velocity = Vector2.zero;
            body.isKinematic = true;
            return;
        }

        if (stationary)
            return;

        body.velocity = new(moveSpeed * (left ? -1 : 1) * (fast ? 1.5f : 1f), body.velocity.y);

        canBounce |= body.velocity.y < 0;
        Collectable |= body.velocity.y < 0;

        HandleCollision();

        if (passthrough && Collectable && body.velocity.y <= 0 && !Utils.IsAnyTileSolidBetweenWorldBox(body.position + worldCollider.offset, worldCollider.size * transform.lossyScale) && !Physics2D.OverlapBox(body.position, Vector2.one / 3, 0, ANY_GROUND_MASK)) {
            passthrough = false;
            gameObject.layer = Layers.LayerEntity;
        }
        if (!passthrough) {
            if (Utils.IsAnyTileSolidBetweenWorldBox(body.position + worldCollider.offset, worldCollider.size * transform.lossyScale)) {
                gameObject.layer = Layers.LayerHitsNothing;
            } else {
                gameObject.layer = Layers.LayerEntity;
            }
        }

        if (photonView.IsMine && (lifespan <= 0 || (!passthrough && body.position.y < GameManager.Instance.GetLevelMinY())))
            photonView.RPC("Crushed", RpcTarget.All);
    }

    void HandleCollision() {
        physics.UpdateCollisions();

        if (physics.hitLeft || physics.hitRight) {
            if (photonView.IsMine)
                photonView.RPC("Turnaround", RpcTarget.All, physics.hitLeft);
            else
                Turnaround(physics.hitLeft);
        }

        if (physics.onGround && canBounce) {
            body.velocity = new(body.velocity.x, bounceAmount);
            if (photonView.IsMine && physics.hitRoof)
                photonView.RPC("Crushed", RpcTarget.All);
        }
    }

    public void DisableAnimator() {
        GetComponent<Animator>().enabled = false;
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
