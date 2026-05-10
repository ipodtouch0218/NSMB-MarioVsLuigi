#nullable enable

using System.Collections.Generic;

namespace NSMB.Replay.Stats
{

    public class PlayerInfo {
        public readonly string PlayerName;

        public int Stars; // StarChasers
        public int PurpleCoins; // CoinRunners

        public int Coins, Lives, Deaths;

        public bool Disconnected;

        // useful info that must be tracked from the simulation
        public int ItemDropCount;
        public readonly List<PointDamage> DamagePoints = new();
        public readonly List<PointDeath> DeathPoints = new();
        public readonly List<PointStarCollected> StarsCollectedPoints = new();
        public readonly List<PointStarLoss> StarsLostPoints = new();
        //public int PurpleCoinsCollected;
        public readonly List<PointCoinCollected> CoinsCollectedPoints = new();
        public readonly List<PointCombo> ComboReceivedPoints = new();

        public PointKnockback? CurrKnockbackPoint = null;
        public readonly List<PointKnockback> KnockbackPoints = new();

        public readonly List<PointPowerupCollect> PowerupCollectPoints = new();

        public PointCombo? CurrComboPoint;

        public int StarsDropped;
        public int PurpleCoinsDropped;

        // for powerUP changes
        public PointPowerChange? CurrPowerChangePoint = null;
        public readonly List<PointPowerChange> PowerChangePoints = new();

        // if Mario is invincible we will create a new starman point
        // if he's not then we will set the current starman point end frame
        public PointStarmanChange? CurrStarmanChangePoint = null;
        public readonly List<PointStarmanChange> StarmanChangePoints = new();

        // these are for reserve items
        public PointReserveChange? CurrReserveChangePoint = null;
        public readonly List<PointReserveChange> ReserveChangePoints = new();

        // when a combo starts, this gets set
        // when this reaches 0 then the combo is over
        // this is needed to count combos where player dies in a pit
        public const int ComboTimerStart = 15;
        public int ComboEndTimer;

        public PlayerInfo(string playerName) {
            PlayerName = playerName;
        }
    }

    public class GlobalInfo {
        public int AttemptedStarSpawns = 0;
        public int SuccessfulStarSpawns = 0;
        public int FailedStarSpawns = 0;

        public PointBigCollectableSpawned? CurrBigCollectable = null;
        public readonly List<PointBigCollectableSpawned> BigCollectablesSpawned = new();
    }
}