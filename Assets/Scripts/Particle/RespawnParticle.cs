using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RespawnParticle : MonoBehaviour {

    float destroytimer = 3, respawnTimer = 1.5f;
    public PlayerController player;

    void Update() {
        if ((destroytimer -= Time.deltaTime) <= 0) {
            GameObject.Destroy(gameObject);
        }
        if (respawnTimer > 0 && (respawnTimer -= Time.deltaTime) <= 0) {
            if (player != null)
                player.photonView.RPC("Respawn", Photon.Pun.RpcTarget.All);
        }
    }
}
