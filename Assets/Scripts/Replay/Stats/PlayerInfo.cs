#nullable enable

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
        public readonly List<PointStarCollected> StarsCollectedPoints = new();
        public readonly List<PointStarLoss> StarsLostPoints = new();
        public readonly List<PointCoinCollected> CoinsCollectedPoints = new();
        public readonly List<PointPowerupCollect> PowerupCollectPoints = new();
        public readonly List<PointTaunt> TauntPoints = new();

        //---more complex points
        public PointKnockback? CurrKnockbackPoint = null;
        public readonly List<PointKnockback> KnockbackPoints = new();

        public PointCombo? CurrComboPoint;
        public readonly List<PointCombo> ComboReceivedPoints = new();

        // for powerUP changes
        public PointPowerChange? CurrPowerChangePoint = null;
        public readonly List<PointPowerChange> PowerChangePoints = new();

        public PointStarmanChange? CurrStarmanChangePoint = null;
        public readonly List<PointStarmanChange> StarmanChangePoints = new();

        public PointStarCountChange? CurrStarCountChangePoint = null;
        public readonly List<PointStarCountChange> StarCountChangePoints = new();

        // these are for reserve items
        public PointReserveChange? CurrReserveChangePoint = null;
        public readonly List<PointReserveChange> ReserveChangePoints = new();

        public readonly List<EntityRef> BlocksBumped = new();
        public readonly List<PointBlockHit> BlockHitPoints = new();

        // when a combo starts, this gets set
        // when this reaches 0 then the combo is over
        // this is needed to count combos where player dies in a pit
        public const int ComboTimerStart = 15;
        public int ComboEndTimer;

        //--methods
        public PlayerInfo(string playerName, PlayerRef playerRef) {
            PlayerName = playerName;
            PlayerRef = playerRef;
        }

        public IEnumerable<TimePoint> GetAllStarPoints() {
            List<TimePoint> starInfoPoints = new();
            starInfoPoints.AddRange(StarsCollectedPoints);
            starInfoPoints.AddRange(StarsLostPoints);
            starInfoPoints.Sort();
            return starInfoPoints;
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
