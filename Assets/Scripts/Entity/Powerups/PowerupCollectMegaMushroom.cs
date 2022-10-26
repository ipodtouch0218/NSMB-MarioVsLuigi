using UnityEngine;

using Fusion;

public class PowerupCollectMegaMushroom : MonoBehaviour, IPowerupCollect {

    public PowerupReserveResult OnPowerupCollect(PlayerController player, MovingPowerup powerup) {
        if (player.State == Enums.PowerupState.MegaMushroom)
            return PowerupReserveResult.ReserveNewPowerup;

        NetworkRunner runner = player.Runner;

        player.previousState = player.State;
        player.State = Enums.PowerupState.MegaMushroom;
        player.powerupFlash = 2;
        player.IsCrouching |= player.ForceCrouchCheck();
        player.IsPropellerFlying = false;
        player.usedPropellerThisJump = false;
        player.IsDrilling &= player.IsSpinnerFlying;
        player.PropellerLaunchTimer = TickTimer.None;

        player.GiantStartTimer = TickTimer.CreateFromSeconds(runner, player.giantStartTime);
        player.GiantTimer = TickTimer.CreateFromSeconds(runner, 15f + player.giantStartTime);
        player.IsInKnockback = false;
        player.IsGroundpounding = false;
        player.IsCrouching = false;
        player.IsPropellerFlying = false;
        player.usedPropellerThisJump = false;
        player.IsSpinnerFlying = false;
        player.IsDrilling = false;
        player.IsInShell = false;
        transform.localScale = Vector3.one;
        Instantiate(PrefabList.Instance.Particle_Giant, transform.position, Quaternion.identity);

        player.PlaySoundEverywhere(powerup.powerupScriptable.soundEffect);
        return PowerupReserveResult.ReserveOldPowerup;
    }
}
