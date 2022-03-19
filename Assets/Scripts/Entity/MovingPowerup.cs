﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class MovingPowerup : MonoBehaviourPun {

    private static int groundMask = -1;
    public float speed, bouncePower, terminalVelocity = 4, blinkingRate = 4;
    private Rigidbody2D body;
    private SpriteRenderer sRenderer;
    private bool right = true;
    public bool passthrough, avoidPlayers;
    public PlayerController followMe;
    public float followMeCounter, despawnCounter = 15, ignoreCounter;
    private PhysicsEntity physics;

    void Start() {
        body = GetComponent<Rigidbody2D>();
        sRenderer = GetComponent<SpriteRenderer>();
        physics = GetComponent<PhysicsEntity>();

        object[] data = photonView.InstantiationData;
        if (data != null) {
            if (data[0] is float ignore) {
                ignoreCounter = ignore;
            } else if (data[0] is int follow) {
                followMe = PhotonView.Find(follow).GetComponent<PlayerController>();
                followMeCounter = 1.5f;
                passthrough = true;
                body.isKinematic = true;
                gameObject.layer = LayerMask.NameToLayer("HitsNothing");
            }
        }

        if (groundMask == -1)
            groundMask = LayerMask.GetMask("Ground", "PassthroughInvalid");
    }

    void LateUpdate() {
        ignoreCounter -= Time.deltaTime;
        if (!followMe) 
            return;

        //Following someone.
        float size = followMe.flying ? 3.8f : 2.8f;
        transform.position = new Vector3(followMe.transform.position.x, followMe.cameraController.currentPosition.y + (size*0.6f));

        sRenderer.enabled = followMeCounter * blinkingRate % 2 > 1;
        if ((followMeCounter -= Time.deltaTime) < 0) {
            followMe = null;
            if (photonView.IsMine) {
                photonView.TransferOwnership(PhotonNetwork.MasterClient);
                passthrough = true;
            }
        }
    }

    void FixedUpdate() {
        if (GameManager.Instance && GameManager.Instance.gameover) {
            body.velocity = Vector2.zero;
            body.isKinematic = true;
            return;
        }
        if (followMe) 
            return;

        despawnCounter -= Time.fixedDeltaTime;
        sRenderer.enabled = !(despawnCounter <= 3 && despawnCounter * blinkingRate % 1 < 0.5f);

        if (despawnCounter <= 0 && photonView.IsMine) {
            photonView.RPC("DespawnWithPoof", RpcTarget.All);
            return;
        }

        body.isKinematic = false;
        if (passthrough) {
            if (!Utils.IsTileSolidAtWorldLocation(body.position) && !Physics2D.OverlapBox(body.position, Vector2.one / 3f, 0, groundMask)) {
                gameObject.layer = LayerMask.NameToLayer("Entity");
                passthrough = false;
            } else {
                return;
            }
        }

        HandleCollision();
        if (avoidPlayers && physics.onGround && !followMe) {
            Collider2D closest = null;
            Vector2 closestPosition = Vector2.zero;
            float distance = float.MaxValue;
            foreach (var hit in Physics2D.OverlapCircleAll(body.position, 10f)) {
                if (!hit.CompareTag("Player")) 
                    continue;
                Vector2 actualPosition = hit.attachedRigidbody.position + hit.offset;
                float tempDistance = Vector2.Distance(actualPosition, body.position);
                if (tempDistance > distance) 
                    continue;
                distance = tempDistance;    
                closest = hit;
                closestPosition = actualPosition;
            }
            if (closest)
                right = (closestPosition.x - body.position.x) < 0;
        }

        body.velocity = new Vector2(body.velocity.x, Mathf.Max(-terminalVelocity, body.velocity.y));
    }
    void HandleCollision() {
        physics.UpdateCollisions();
        if (physics.hitLeft || physics.hitRight) {
            right = physics.hitLeft;
            body.velocity = new Vector2(speed * (right ? 1 : -1), body.velocity.y);
        }
        if (physics.onGround) {
            body.velocity = new Vector2(speed * (right ? 1 : -1), bouncePower);
            if (physics.hitRoof) {
                photonView.RPC("DespawnWithPoof", RpcTarget.All);
                return;
            }
        }
    }

    [PunRPC]
    public void DespawnWithPoof() {
        if (photonView.IsMine)
            PhotonNetwork.Destroy(gameObject);
        Instantiate(Resources.Load("Prefabs/Particle/Puff"), transform.position, Quaternion.identity);
    }
}
