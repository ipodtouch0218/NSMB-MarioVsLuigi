using UnityEngine;

using Fusion;

public class PowerupCollectStarman : MonoBehaviour, IPowerupCollect {

    public PowerupReserveResult OnPowerupCollect(PlayerController player, MovingPowerup powerup) {

        NetworkRunner runner = player.Runner;

        if (!player.IsStarmanInvincible)
            player.StarCombo = 0;

        player.StarmanTimer = TickTimer.CreateFromSeconds(runner, 10f);
        player.PlaySound(powerup.powerupScriptable.soundEffect);

        if (player.HeldEntity) {
            player.HeldEntity.SpecialKill(player.FacingRight, false, 0);
            player.SetHolding(null);
        }

        return PowerupReserveResult.None;
    }
}
