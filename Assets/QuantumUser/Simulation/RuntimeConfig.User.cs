namespace Quantum {
  public partial class RuntimeConfig {

        public byte StarsToWin = 10;
        public byte CoinsForPowerup = 8;
        public byte Lives = 0;
        public bool LivesEnabled => Lives > 0;
        public int TimerSeconds = 0;
        public bool TimerEnabled => TimerSeconds > 0;

        public bool TeamsEnabled = false;
        public bool CustomPowerupsEnabled = true;

        public byte ExpectedPlayers;

    }
}