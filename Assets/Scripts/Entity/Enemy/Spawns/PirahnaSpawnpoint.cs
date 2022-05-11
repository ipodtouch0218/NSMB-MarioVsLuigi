using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PirahnaSpawnpoint : EnemySpawnpoint {

    private PiranhaPlantController plant;

    void Start() {
        plant = GetComponent<PiranhaPlantController>();
    }

    public override bool AttemptSpawning() {
        plant.photonView.RPC("Respawn", Photon.Pun.RpcTarget.All);
        return true;
    }
}
