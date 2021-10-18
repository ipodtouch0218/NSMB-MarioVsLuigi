using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RespawnParticle : MonoBehaviour {

    float respawnTimer = 1.5f;
    public PlayerController player;

    void Update() {
        if (respawnTimer > 0 && (respawnTimer -= Time.deltaTime) <= 0) {
            if (player != null)
                player.photonView.RPC("Respawn", Photon.Pun.RpcTarget.All);
        }
    }
}
