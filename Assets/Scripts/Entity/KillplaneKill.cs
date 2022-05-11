using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class KillplaneKill : MonoBehaviourPun {
    [SerializeField] float killTime = 0f;
    private float timer = 0;
    void Update() {
        if (transform.position.y >= GameManager.Instance.GetLevelMinY()) 
            return;
        timer += Time.deltaTime;
        if (timer < killTime)
            return;
        if (!photonView) {
            Destroy(gameObject);
            return;
        } 
        if (photonView.IsMine) {
            PhotonNetwork.Destroy(photonView);
            return;
        }
    }
}
