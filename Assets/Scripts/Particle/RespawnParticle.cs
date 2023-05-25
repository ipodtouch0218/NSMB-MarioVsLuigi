using UnityEngine;

using NSMB.Entities.Player;

public class RespawnParticle : MonoBehaviour {

    public PlayerController player;

    public void Start() {
        foreach (ParticleSystem system in GetComponentsInChildren<ParticleSystem>()) {
            ParticleSystem.MainModule main = system.main;
            main.startColor = player.animationController.GlowColor;

            system.Play();
        }
    }
}
