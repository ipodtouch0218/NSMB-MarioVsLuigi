using UnityEngine;

using NSMB.Entities.Player;

namespace NSMB.Entities.Collectable.Powerups {
    public class PowerupCollect1Up : MonoBehaviour, IPowerupCollect {

        private GameObject particle;

        public PowerupReserveResult OnPowerupCollect(PlayerController player, PowerupScriptable powerup) {
            player.Lives++;

            if (!particle)
                particle = Instantiate(PrefabList.Instance.Particle_1Up, transform.position, Quaternion.identity);

            return PowerupReserveResult.None;
        }
    }
}
