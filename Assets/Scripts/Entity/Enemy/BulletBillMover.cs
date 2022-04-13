using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class BulletBillMover : KillableEntity {
    
    public float speed, playerSearchRadius = 4;
    private Vector2 searchVector;
    new void Start() {
        base.Start();
        searchVector = new Vector2(playerSearchRadius * 2, 100);
        left = photonView && photonView.InstantiationData != null && (bool) photonView.InstantiationData[0];
        body.velocity = new Vector2(speed * (left ? -1 : 1), body.velocity.y);

        Transform t = transform.GetChild(1);
        ParticleSystem ps = t.GetComponent<ParticleSystem>();
        ParticleSystem.ShapeModule shape = ps.shape;
        if (!left) {
            Transform tf = transform.GetChild(0);
            tf.localPosition *= new Vector2(-1, 1);
            shape.rotation = new Vector3(0, 0, -33);
        }

        ps.Play();
        sRenderer.flipX = !left;
    }

    void FixedUpdate() {
        if (GameManager.Instance && GameManager.Instance.gameover) {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0;
            animator.enabled = false;
            body.isKinematic = true;
            return;
        }
        if (frozen) {
            body.velocity = new Vector2(0, 0);
        } else {
            body.velocity = new Vector2(speed * (left ? -1 : 1), body.velocity.y);
        }
        if (photonView.IsMine)
            DespawnCheck();
    }
    public override void InteractWithPlayer(PlayerController player) {
        if (player.frozen)
            return;

        Vector2 damageDirection = (player.body.position - body.position).normalized;
        bool attackedFromAbove = Vector2.Dot(damageDirection, Vector2.up) > 0.5f;

        if (player.invincible > 0 || player.inShell || player.sliding
            || ((player.groundpound || player.drill) && player.state != Enums.PowerupState.MiniMushroom && attackedFromAbove)
            || player.state == Enums.PowerupState.MegaMushroom) {

            photonView.RPC("SpecialKill", RpcTarget.All, player.body.velocity.x > 0, player.groundpound);
            return;
        }
        if (attackedFromAbove) {
            if (player.state == Enums.PowerupState.MiniMushroom && !player.drill && !player.groundpound) {
                player.groundpound = false;
                player.bounce = true;
            } else {
                photonView.RPC("Kill", RpcTarget.All);
                player.groundpound = false;
                player.bounce = !player.drill;
            }
            player.photonView.RPC("PlaySound", RpcTarget.All, "enemy/goomba");
            player.drill = false;
            return;
        }

        player.photonView.RPC("Powerdown", RpcTarget.All, false);
        // left = damageDirection.x < 0;
    }

    void DespawnCheck() {
        foreach (var collision in Physics2D.BoxCastAll(body.position, searchVector, 0f, Vector2.zero)) {
            if (collision.transform.CompareTag("Player"))
                return;
        }
        //left border check
        if (body.position.x - playerSearchRadius < GameManager.Instance.GetLevelMinX()) {
            foreach (var collision in Physics2D.BoxCastAll(body.position + new Vector2(GameManager.Instance.levelWidthTile * 0.5f, 0), searchVector, 0f, Vector2.zero)) {
                if (collision.transform.CompareTag("Player"))
                    return;
            }
        }
        //right border check
        if (body.position.x + playerSearchRadius > GameManager.Instance.GetLevelMaxX()) {
            foreach (var collision in Physics2D.BoxCastAll(body.position - new Vector2(GameManager.Instance.levelWidthTile * 0.5f, 0), searchVector, 0f, Vector2.zero)) {
                if (collision.transform.CompareTag("Player"))
                    return;
            }
        }
        PhotonNetwork.Destroy(photonView);
    }

    [PunRPC]
    public override void Kill() {
        SpecialKill(!left, false);
    }
    
    [PunRPC]
    public override void SpecialKill(bool right, bool groundpound) {
        body.velocity = new Vector2(0, 2.5f);
        body.constraints = RigidbodyConstraints2D.None;
        body.angularVelocity = 400f * (right ? 1 : -1);
        body.gravityScale = 1.5f;
        body.isKinematic = false;
        hitbox.enabled = false;
        animator.speed = 0;
        gameObject.layer = LayerMask.NameToLayer("HitsNothing");
        if (groundpound)
            Instantiate(Resources.Load("Prefabs/Particle/EnemySpecialKill"), body.position + new Vector2(0, 0.5f), Quaternion.identity);

        dead = true;
        photonView.RPC("PlaySound", RpcTarget.All, "enemy/shell_kick");
    } 
    void OnDrawGizmosSelected() {
        if (!GameManager.Instance) 
            return;
        Gizmos.color = new Color(1, 0, 0, 0.5f);
        Gizmos.DrawCube(body.position, searchVector);
        //left border check
        if (body.position.x - playerSearchRadius < GameManager.Instance.GetLevelMinX())
            Gizmos.DrawCube(body.position + new Vector2(GameManager.Instance.levelWidthTile * 0.5f, 0), searchVector);
        //right border check
        if (body.position.x + playerSearchRadius > GameManager.Instance.GetLevelMaxX())
            Gizmos.DrawCube(body.position - new Vector2(GameManager.Instance.levelWidthTile * 0.5f, 0), searchVector);
    }
}