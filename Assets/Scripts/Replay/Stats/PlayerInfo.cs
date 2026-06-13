using Quantum;
using System.Collections.Generic;


namespace NSMB.Replay.Stats {
    public class PlayerInfo {
        //---player info
        public readonly PlayerRef PlayerRef;
        public readonly string PlayerName;

        //---tracking information
        public int Stars, Coins;
        public int StarsDropped, PurpleCoinsDropped;

        // useful info that must be tracked from the simulation
        public readonly List<PointDamage> DamagePoints = new();
        public readonly List<PointDeath> DeathPoints = new();
        public readonly List<PointCoinCollected> CoinsCollectedPoints = new();
        public readonly List<PointPowerupCollect> PowerupCollectPoints = new();
        public readonly List<PointTaunt> TauntPoints = new();

        public readonly List<PointKnockback> KnockbackPoints = new();
        public readonly List<PointCombo> ComboReceivedPoints = new();
        public readonly List<PointPowerChange> PowerChangePoints = new();
        public readonly List<PointStarmanChange> StarmanChangePoints = new();
        public readonly List<PointStarCountChange> StarCountChangePoints = new();
        public readonly List<PointReserveChange> ReserveChangePoints = new();

        //---more complex points
        public PointKnockback CurrKnockbackPoint;
        public PointCombo CurrComboPoint;
        public PointPowerChange CurrPowerChangePoint;
        public PointStarmanChange CurrStarmanChangePoint;
        public PointStarCountChange CurrStarCountChangePoint;
        public PointReserveChange CurrReserveChangePoint;

        public readonly List<EntityRef> BlocksBumped = new();
        public readonly List<PointBlockHit> BlockHitPoints = new();

        // when a combo starts, this gets set
        // when this reaches 0 then the combo is over
        // this is needed to count combos where player dies in a pit
        public const int ComboTimerStart = 15;
        public int ComboEndTimer;

        //---misc
        public PointCombo LastComboPointForDeath;
        public PointKnockback LastKnockbackPointForDeath;

        //--methods
        public PlayerInfo(string playerName, PlayerRef playerRef) {
            PlayerName = playerName;
            PlayerRef = playerRef;
        }

        public IEnumerable<TimePoint> GetAllItemSpawnPoints() {
            List<TimePoint> itemSpawnPoints = new();
            foreach (var point in CoinsCollectedPoints) {
                // skip no item drops
                if (point.CoinItem == null) {
                    continue;
                }

                itemSpawnPoints.Add(point);
            }

            foreach (var point in BlockHitPoints) {
                // skip no item drops
                if (point.SpawnedItem == null) {
                    continue;
                }

                itemSpawnPoints.Add(point);
            }

            itemSpawnPoints.Sort();
            return itemSpawnPoints;
        }
    }
}
