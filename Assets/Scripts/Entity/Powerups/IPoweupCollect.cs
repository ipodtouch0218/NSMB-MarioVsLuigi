using NSMB.Entities.Player;

namespace NSMB.Entities.Collectable.Powerups {
    public interface IPowerupCollect {

        public PowerupReserveResult OnPowerupCollect(PlayerController player, PowerupScriptable powerup);
    }

    public enum PowerupReserveResult {
        None,
        NoneButPlaySound,
        ReserveOldPowerup,
        ReserveNewPowerup,
    }
}
