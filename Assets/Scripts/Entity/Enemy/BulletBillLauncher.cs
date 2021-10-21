using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class BulletBillLauncher : MonoBehaviourPun {
    public float playerSearchRadius = 7, playerCloseCutoff = 1;
    public float initialShootTimer = 5;
    private float shootTimer;
    new AudioSource audio;
    private List<GameObject> bills = new List<GameObject>();

    private Vector2 searchBox;
    private Vector3 offset = new Vector3(0.25f, -0.2f, 0);

    void Start() {
        audio = GetComponent<AudioSource>();

        searchBox = new Vector2(playerSearchRadius, playerSearchRadius);
    }
    void Update() {
        if (!PhotonNetwork.IsMasterClient) return;

        if ((shootTimer -= Time.deltaTime) <= 0) {
            shootTimer = initialShootTimer;
            TryToShoot();
        }
    }

    void TryToShoot() {
        for (int i = 0; i < bills.Count; i++) {
            if (bills[i] == null)
                bills.RemoveAt(i--);
        }
        if (bills.Count >= 3) return;

        //Shoot left
        if (IntersectsPlayer(transform.position - new Vector3(playerSearchRadius/2 + 1, 0, 0))) {
            GameObject newBill = PhotonNetwork.InstantiateRoomObject("Prefabs/Enemy/BulletBill", transform.position + new Vector3(-offset.x, offset.y, offset.z), Quaternion.identity, 0, new object[]{true});
            bills.Add(newBill);
            return;
        }

        //Shoot right
        if (IntersectsPlayer(transform.position + new Vector3(playerSearchRadius/2 + 1, 0, 0))) {
            GameObject newBill = PhotonNetwork.InstantiateRoomObject("Prefabs/Enemy/BulletBill", transform.position + new Vector3(offset.x, offset.y, offset.z), Quaternion.identity, 0, new object[]{false});
            bills.Add(newBill);
            return;
        }
    }

    bool IntersectsPlayer(Vector3 origin) {
        foreach (var hit in Physics2D.OverlapBoxAll(origin, searchBox, 0)) {
            if (hit.gameObject.tag == "Player" || hit.gameObject.tag == "CameraTarget") {
                //player in radius, see if TOO close
                if (Vector3.Distance(hit.transform.position, transform.position) <= 1) {
                    return false;
                }
                return true;
            }
        }
        return false;
    }

    void OnDrawGizmosSelected() {
        Gizmos.color = new Color(1, 0, 0, 0.5f);
        Gizmos.DrawCube(transform.position - new Vector3(playerSearchRadius/2 + 1, 0, 0), searchBox);
        Gizmos.color = new Color(0, 0, 1, 0.5f);
        Gizmos.DrawCube(transform.position + new Vector3(playerSearchRadius/2 + 1, 0, 0), searchBox);
    }
}