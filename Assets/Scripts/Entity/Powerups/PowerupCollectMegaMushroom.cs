using UnityEngine;

using Fusion;
using NSMB.Entities.Player;

namespace NSMB.Entities.Collectable.Powerups {
    public class PowerupCollectMegaMushroom : MonoBehaviour, IPowerupCollect {

        private GameObject particle;

        public PowerupReserveResult OnPowerupCollect(PlayerController player, MovingPowerup powerup) {
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

            player.GiantStartTimer = TickTimer.CreateFromSeconds(runner, player.giantStartTime);
            player.GiantTimer = TickTimer.CreateFromSeconds(runner, 15f + player.giantStartTime);
            player.IsInKnockback = false;
            player.IsGroundpounding = false;
            player.IsCrouching = false;
            player.IsPropellerFlying = false;
            player.UsedPropellerThisJump = false;
            player.IsSpinnerFlying = false;
            player.IsDrilling = false;
            player.IsInShell = false;
            transform.localScale = Vector3.one;

            if (!particle)
                particle = Instantiate(PrefabList.Instance.Particle_Giant, player.transform.position, Quaternion.identity);

            player.PlaySoundEverywhere(powerup.powerupScriptable.soundEffect);
            return PowerupReserveResult.ReserveOldPowerup;
        }
    }
}
