using UnityEngine;

public class PowerupCollect1Up : MonoBehaviour, IPowerupCollect {
    public PowerupReserveResult OnPowerupCollect(PlayerController player, MovingPowerup powerup) {
        player.Lives++;

        Instantiate(PrefabList.Instance.Particle_1Up, transform.position, Quaternion.identity);
        player.PlaySound(powerup.powerupScriptable.soundEffect);

        return PowerupReserveResult.None;
    }
}
