using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class KillplaneKill : MonoBehaviour {
    void Update() {
        if (transform.position.y < GameManager.Instance.GetLevelMinY()) {
            PhotonView view = GetComponent<PhotonView>();
            if (view == null) {
                GameObject.Destroy(gameObject);
                return;
            } 
            if (view.IsMine) {
                PhotonNetwork.Destroy(view);
            }
        }
    }
}
