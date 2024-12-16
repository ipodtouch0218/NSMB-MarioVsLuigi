using System;

namespace Quantum {
    [Serializable]
    public unsafe partial struct GameRules {

        public bool IsLivesEnabled => Lives > 0;
        public bool IsTimerEnabled => TimerSeconds > 0;

    }
}