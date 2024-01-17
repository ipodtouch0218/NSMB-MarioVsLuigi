using UnityEngine;

using Fusion;
using NSMB.Entities.Player;

namespace NSMB.Entities.Collectable.Powerups {
    public class PowerupCollectMegaMushroom : MonoBehaviour, IPowerupCollect {

        public PowerupReserveResult OnPowerupCollect(PlayerController player, PowerupScriptable powerup) {
            if (player.State == Enums.PowerupState.MegaMushroom) {
                return PowerupReserveResult.ReserveNewPowerup;
            }

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

            return PowerupReserveResult.ReserveOldPowerup;
        }
    }
}
