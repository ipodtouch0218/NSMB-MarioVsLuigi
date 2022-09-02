using UnityEngine;

using Photon.Pun;
using NSMB.Utils;

public class BulletBillMover : KillableEntity {

    public float speed, playerSearchRadius = 4, despawnDistance = 8;
    private Vector2 searchVector;

    public new void Start() {
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

    public new void FixedUpdate() {
        if (GameManager.Instance && GameManager.Instance.gameover) {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0;
            animator.enabled = false;
            body.isKinematic = true;
            return;
        }
        if (Frozen) {
            body.velocity = Vector2.zero;
        } else {
            body.velocity = new(speed * (left ? -1 : 1), body.velocity.y);
        }

        if (!Frozen && photonView.IsMine )
            DespawnCheck();
    }
    public override void InteractWithPlayer(PlayerController player) {
        if (Frozen || player.Frozen)
            return;

        Vector2 damageDirection = (player.body.position - body.position).normalized;
        bool attackedFromAbove = Vector2.Dot(damageDirection, Vector2.up) > 0.5f;

        if (player.invincible > 0 || player.inShell || player.sliding
            || ((player.groundpound || player.drill) && player.state != Enums.PowerupState.MiniMushroom && attackedFromAbove)
            || player.state == Enums.PowerupState.MegaMushroom) {

            if (player.drill) {
                player.bounce = true;
                player.drill = false;
            }
            photonView.RPC(nameof(Kill), RpcTarget.All);
            return;
        }
        if (attackedFromAbove) {
            if (!(player.state == Enums.PowerupState.MiniMushroom && !player.groundpound)) {
                photonView.RPC(nameof(Kill), RpcTarget.All);
            }
            player.photonView.RPC(nameof(PlayerController.PlaySound), RpcTarget.All, Enums.Sounds.Enemy_Generic_Stomp);
            player.drill = false;
            player.groundpound = false;
            player.bounce = true;
            return;
        }

        player.photonView.RPC(nameof(PlayerController.Powerdown), RpcTarget.All, false);
        // left = damageDirection.x < 0;
    }

    private void DespawnCheck() {
        foreach (PlayerController player in GameManager.Instance.players) {
            if (!player)
                continue;

            if (Utils.WrappedDistance(player.body.position, body.position) < despawnDistance)
                return;
        }

        PhotonNetwork.Destroy(photonView);
    }

    [PunRPC]
    public override void Kill() {
        SpecialKill(!left, false, 0);
    }

    [PunRPC]
    public override void SpecialKill(bool right, bool groundpound, int combo) {
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
        PlaySound(Enums.Sounds.Enemy_Shell_Kick);
    }

    public void OnDrawGizmosSelected() {
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