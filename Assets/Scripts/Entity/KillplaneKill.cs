using UnityEngine;
using Photon.Pun;

public class KillplaneKill : MonoBehaviourPun {

    [SerializeField] private float killTime = 0f;
    private float timer = 0;

    public void Update() {
        if (transform.position.y >= GameManager.Instance.GetLevelMinY())
            return;

        if ((timer += Time.deltaTime) < killTime)
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
