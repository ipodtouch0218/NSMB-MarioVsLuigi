using UnityEngine;

public class PowerupCollect1Up : MonoBehaviour, IPowerupCollect {

    private GameObject particle;
    public PowerupReserveResult OnPowerupCollect(PlayerController player, MovingPowerup powerup) {
        player.Lives++;

        particle = Instantiate(PrefabList.Instance.Particle_1Up, transform.position, Quaternion.identity);

        return PowerupReserveResult.None;
    }
}
