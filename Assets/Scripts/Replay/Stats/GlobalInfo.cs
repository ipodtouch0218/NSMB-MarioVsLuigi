#nullable enable

using Quantum;
using System.Collections.Generic;

namespace NSMB.Replay.Stats {
    public class GlobalInfo {
        public int AttemptedStarSpawns = 0;
        public int SuccessfulStarSpawns = 0;
        public int FailedStarSpawns = 0;

        public PointBigCollectableSpawned? CurrBigCollectable = null;
        public readonly List<PointBigCollectableSpawned> BigCollectablesSpawned = new();
    }
}