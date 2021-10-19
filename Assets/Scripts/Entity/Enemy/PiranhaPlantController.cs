using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class PiranhaPlantController : KillableEntity {
    public Vector2 playerDetectSize = new Vector2(3,3);
    public float popupTimerRequirement = 6f;
    public float popupTimer;
    private new BoxCollider2D collider2D;

    public new void Start() {
        base.Start();
        collider2D = GetComponent<BoxCollider2D>();
    }

    public new void Update() {
        if (GameManager.Instance && GameManager.Instance.gameover) {
            body.velocity = Vector2.zero;
            animator.enabled = false;
            body.isKinematic = true;
            return;
        }
        base.Update();

        animator.SetBool("dead", dead);
        if (dead || (photonView && !photonView.IsMine))
            return;

        foreach (var hit in Physics2D.OverlapBoxAll(transform.transform.position + (Vector3) (playerDetectSize*new Vector2(0, 1/2f)), playerDetectSize, 0)) {
            if (hit.transform.tag == "Player" || hit.transform.tag == "CameraTarget") {
                return;
            }
        }

        if ((popupTimer += Time.deltaTime) >= popupTimerRequirement) {
            animator.SetTrigger("popup");
            popupTimer = 0;
        }
    }

    public void EnableHitbox() {
        collider2D.enabled = true;
    }
    public void DisableHitbox() {
        collider2D.enabled = false;
    }

    [PunRPC]
    public void Respawn() {
        dead = false;
        popupTimer = 3;
    }
    [PunRPC]
    public void Kill() {
        PlaySound("enemy/shell_kick");
        PlaySound("enemy/piranhaplant-die");
        dead = true;
        Instantiate(Resources.Load("Prefabs/Particle/Puff"), transform.position + new Vector3(0, 0.5f, 0), Quaternion.identity);
        if (photonView.IsMine) {
            PhotonNetwork.Instantiate("Prefabs/LooseCoin", transform.position + new Vector3(0, 1f, 0), Quaternion.identity);
        }
    }
    
    void OnDrawGizmosSelected() {
        Gizmos.color = new Color(1, 0, 0, 0.5f);
        Gizmos.DrawCube(transform.transform.position + (Vector3) (playerDetectSize*new Vector2(0, 1/2f)), playerDetectSize);
    }
}
