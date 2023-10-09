using UnityEngine;

using Fusion;
using NSMB.Entities.Player;

namespace NSMB.Entities.Collectable.Powerups {
    public class PowerupCollectMegaMushroom : MonoBehaviour, IPowerupCollect {

        private GameObject particle;

        public PowerupReserveResult OnPowerupCollect(PlayerController player, PowerupScriptable powerup) {
            if (player.State == Enums.PowerupState.MegaMushroom)
                return PowerupReserveResult.ReserveNewPowerup;

            NetworkRunner runner = player.Runner;

            player.PreviousState = player.State;
            player.State = Enums.PowerupState.MegaMushroom;
            player.powerupFlash = 2;
            player.IsCrouching |= player.ForceCrouchCheck();
            player.IsPropellerFlying = false;
            player.UsedPropellerThisJump = false;
            player.IsDrilling &= player.IsSpinnerFlying;
            player.PropellerLaunchTimer = TickTimer.None;

            player.MegaStartTimer = TickTimer.CreateFromSeconds(runner, player.megaStartTime);
            player.IsInKnockback = false;
            player.IsGroundpounding = false;
            player.IsCrouching = false;
            player.IsPropellerFlying = false;
            player.UsedPropellerThisJump = false;
            player.IsSpinnerFlying = false;
            player.IsDrilling = false;
            player.IsInShell = false;

            player.AttemptThrowHeldItem();

            if (!particle) {
                Vector3 position = player.transform.position;
                position.z = -4;
                particle = Instantiate(PrefabList.Instance.Particle_Giant, position, Quaternion.identity);
            }

            return PowerupReserveResult.ReserveOldPowerup;
        }
    }
}
