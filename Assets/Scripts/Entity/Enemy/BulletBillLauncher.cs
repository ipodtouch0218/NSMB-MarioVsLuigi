using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using NSMB.Utils;

public class BulletBillLauncher : MonoBehaviourPun {

    public float playerSearchRadius = 7, playerCloseCutoff = 1;
    public float initialShootTimer = 5;
    private float shootTimer;
    private readonly List<GameObject> bills = new();

    private Vector2 searchBox, closeSearchBox = new(1.5f, 1f), searchOffset, spawnOffset = new(0.25f, -0.2f);

    void Start() {
        searchBox = new(playerSearchRadius, playerSearchRadius);
        searchOffset = new(playerSearchRadius/2 + playerCloseCutoff, 0);
    }
    void Update() {
        if (!PhotonNetwork.IsMasterClient || GameManager.Instance.gameover)
            return;

        if ((shootTimer -= Time.deltaTime) <= 0) {
            shootTimer = initialShootTimer;
            TryToShoot();
        }
    }

    void TryToShoot() {
        if (!Utils.IsTileSolidAtWorldLocation(transform.position))
            return;
        for (int i = 0; i < bills.Count; i++) {
            if (bills[i] == null)
                bills.RemoveAt(i--);
        }
        if (bills.Count >= 3)
            return;

        //Check for players close by
        if (IntersectsPlayer(transform.position + Vector3.down * 0.25f, closeSearchBox))
            return;

        //Shoot left
        if (IntersectsPlayer((Vector2) transform.position - searchOffset, searchBox)) {
            GameObject newBill = PhotonNetwork.InstantiateRoomObject("Prefabs/Enemy/BulletBill", transform.position + new Vector3(-spawnOffset.x, spawnOffset.y), Quaternion.identity, 0, new object[]{ true });
            bills.Add(newBill);
            return;
        }

        //Shoot right
        if (IntersectsPlayer((Vector2) transform.position + searchOffset, searchBox)) {
            GameObject newBill = PhotonNetwork.InstantiateRoomObject("Prefabs/Enemy/BulletBill", transform.position + new Vector3(spawnOffset.x, spawnOffset.y), Quaternion.identity, 0, new object[]{ false });
            bills.Add(newBill);
            return;
        }
    }

    bool IntersectsPlayer(Vector2 origin, Vector2 searchBox) {
        foreach (var hit in Physics2D.OverlapBoxAll(origin, searchBox, 0)) {
            if (hit.gameObject.CompareTag("Player"))
                return true;
        }
        return false;
    }

    void OnDrawGizmosSelected() {
        Gizmos.color = new Color(1, 0, 0, 0.5f);
        Gizmos.DrawCube(transform.position + Vector3.down * 0.5f, closeSearchBox);
        Gizmos.color = new Color(0, 0, 1, 0.5f);
        Gizmos.DrawCube((Vector2) transform.position - searchOffset, searchBox);
        Gizmos.DrawCube((Vector2) transform.position + searchOffset, searchBox);
    }
}