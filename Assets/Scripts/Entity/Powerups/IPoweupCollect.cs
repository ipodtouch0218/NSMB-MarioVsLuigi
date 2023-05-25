using NSMB.Entities.Player;

namespace NSMB.Entities.Collectable.Powerups {
    public interface IPowerupCollect {

        public PowerupReserveResult OnPowerupCollect(PlayerController player, MovingPowerup powerup);
    }

    public enum PowerupReserveResult {
        None,
        NoneButPlaySound,
        ReserveOldPowerup,
        ReserveNewPowerup,
    }
}
