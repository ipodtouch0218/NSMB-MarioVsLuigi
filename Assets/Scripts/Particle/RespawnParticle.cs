using UnityEngine;

public class RespawnParticle : MonoBehaviour {

    [SerializeField] float respawnTimer = 1.5f;
    public PlayerController player;

    public void Start() {
        foreach (ParticleSystem system in GetComponentsInChildren<ParticleSystem>()) {
            ParticleSystem.MainModule main = system.main;
            main.startColor = player.animationController.glowColor;

            system.Play();
        }
    }

    public void Update() {
        if (!player || !player.photonView.IsMine) 
            return;

        if (respawnTimer > 0 && (respawnTimer -= Time.deltaTime) <= 0)
            player.photonView.RPC("Respawn", Photon.Pun.RpcTarget.All);
    }
}
