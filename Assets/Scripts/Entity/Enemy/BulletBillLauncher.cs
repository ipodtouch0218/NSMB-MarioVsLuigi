using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class BulletBillLauncher : MonoBehaviourPun {

    public float playerSearchRadius = 7, playerCloseCutoff = 1;
    public float initialShootTimer = 5;
    private float shootTimer;
    private readonly List<GameObject> bills = new();

    private Vector2 searchBox, searchOffset, spawnOffset = new(0.25f, -0.2f);

    void Start() {
        searchBox = new(playerSearchRadius, playerSearchRadius);
        searchOffset = new(playerSearchRadius/2 + playerCloseCutoff, 0);
    }
    void Update() {
        if (GameManager.Instance && GameManager.Instance.gameover)
            return;

        if ((shootTimer -= Time.deltaTime) <= 0) {
            shootTimer = initialShootTimer;
            TryToShoot();
        }
    }

    void TryToShoot() {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (!Utils.IsTileSolidAtWorldLocation(transform.position))
            return;

        for (int i = 0; i < bills.Count; i++) {
            if (bills[i] == null)
                bills.RemoveAt(i--);
        }
        if (bills.Count >= 3)
            return;

        //Shoot left
        if (IntersectsPlayer((Vector2) transform.position - searchOffset)) {
            GameObject newBill = PhotonNetwork.InstantiateRoomObject("Prefabs/Enemy/BulletBill", transform.position + new Vector3(-spawnOffset.x, spawnOffset.y), Quaternion.identity, 0, new object[]{ true });
            bills.Add(newBill);
            return;
        }

        //Shoot right
        if (IntersectsPlayer((Vector2)transform.position + searchOffset)) {
            GameObject newBill = PhotonNetwork.InstantiateRoomObject("Prefabs/Enemy/BulletBill", transform.position + new Vector3(spawnOffset.x, spawnOffset.y), Quaternion.identity, 0, new object[]{ false });
            bills.Add(newBill);
            return;
        }
    }

    bool IntersectsPlayer(Vector2 origin) {
        foreach (var hit in Physics2D.OverlapBoxAll(origin, searchBox, 0)) {
            if (hit.gameObject.CompareTag("Player"))
                return true;
        }
        return false;
    }

    void OnDrawGizmosSelected() {
        Gizmos.color = new Color(1, 0, 0, 0.5f);
        Gizmos.DrawCube((Vector2) transform.position - searchOffset, searchBox);
        Gizmos.color = new Color(0, 0, 1, 0.5f);
        Gizmos.DrawCube((Vector2) transform.position + searchOffset, searchBox);
    }
}