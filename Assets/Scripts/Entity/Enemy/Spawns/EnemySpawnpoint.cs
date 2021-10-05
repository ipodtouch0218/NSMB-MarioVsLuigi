using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class EnemySpawnpoint : MonoBehaviour {
    
    [SerializeField] public GameObject prefab;
    public GameObject currentEntity;

    public bool AttemptSpawning() {
        if (currentEntity)
            return false;

        foreach (var hit in Physics2D.OverlapCircleAll(transform.position, 2)) {
            if (hit.gameObject.tag == "Player" || hit.gameObject.tag == "CameraTarget") {
                //cant spawn here
                return false;
            }
        }

        currentEntity = PhotonNetwork.InstantiateRoomObject(prefab.name, transform.position, transform.rotation);
        return true;
    }
}
