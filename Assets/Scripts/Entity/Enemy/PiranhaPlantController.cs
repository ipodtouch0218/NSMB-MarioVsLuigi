using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class PiranhaPlantController : KillableEntity {
    public Vector2 playerDetectSize = new(3,3);
    public float popupTimerRequirement = 6f;
    public float popupTimer;
    private bool upsideDown;

    bool wasFrozen;
    public new void Start() {
        base.Start();
        upsideDown = transform.eulerAngles.z != 0;
    }

    public void Update() {
        if (GameManager.Instance && GameManager.Instance.gameover) {
            animator.enabled = false;
            return;
        }

        if (GameManager.Instance && !GameManager.Instance.musicEnabled)
            return;

        left = false;

        if (frozen)
            wasFrozen = true;

        if (photonView && !dead && photonView.IsMine && Utils.GetTileAtWorldLocation(transform.position + (Vector3.down * 0.1f)) == null) {
            photonView.RPC("Kill", RpcTarget.All);
            return;
        }

        animator.SetBool("dead", dead);
        if (dead || (photonView && !photonView.IsMine))
            return;

        foreach (var hit in Physics2D.OverlapBoxAll(transform.transform.position + (Vector3) (playerDetectSize * new Vector2(0, upsideDown ? -0.5f : 0.5f)), playerDetectSize, transform.eulerAngles.z)) {
            if (hit.transform.CompareTag("Player"))
                return;
        }

        if ((popupTimer += Time.deltaTime) >= popupTimerRequirement) {
            animator.SetTrigger("popup");
            popupTimer = 0;
        }
    }

    public override void InteractWithPlayer(PlayerController player) {
        if (player.invincible > 0 || player.inShell || player.state == Enums.PowerupState.MegaMushroom) {
            photonView.RPC("Kill", RpcTarget.All);
        } else {
            player.photonView.RPC("Powerdown", RpcTarget.All, false);
        }
    }

    [PunRPC]
    public void Respawn() {
        if (frozen)
            return;
        wasFrozen = false;
        dead = false;
        popupTimer = 3;
        animator.Play("end");

        hitbox.enabled = true;
    }

    [PunRPC]
    public override void Kill() {

        if (!wasFrozen) {
            PlaySound(Enums.Sounds.Enemy_Generic_Kick);
            PlaySound(Enums.Sounds.Enemy_PiranhaPlant_Death);
        } else {
            photonView.RPC("PlaySound", RpcTarget.All, Enums.Sounds.Enemy_Generic_FreezeShatter);
        }

        dead = true;
        hitbox.enabled = false;
        Instantiate(Resources.Load("Prefabs/Particle/Puff"), transform.position + new Vector3(0, upsideDown ? -0.5f : 0.5f, 0), Quaternion.identity);
        if (photonView.IsMine)
            PhotonNetwork.Instantiate("Prefabs/LooseCoin", transform.position + new Vector3(0, upsideDown ? -1f : 1f, 0), Quaternion.identity);
    }
    
    void OnDrawGizmosSelected() {
        Gizmos.color = new Color(1, 0, 0, 0.5f);
        Gizmos.DrawCube(transform.position + (Vector3) (playerDetectSize * new Vector2(0, transform.eulerAngles.z != 0 ? -0.5f : 0.5f)), playerDetectSize);
    }
}
