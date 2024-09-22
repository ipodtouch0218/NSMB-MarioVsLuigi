using Photon.Deterministic;

namespace Quantum {
    public class CommandChangeRules : DeterministicCommand {

        public Changes EnabledChanges;

        public AssetRef<Map> Level;
        public byte StarsToWin;
        public byte CoinsForPowerup;
        public byte Lives;
        public ushort TimerSeconds;
        public bool TeamsEnabled;
        public bool CustomPowerupsEnabled;
        public bool DrawOnTimeUp;

        public override void Serialize(BitStream stream) {
            ushort changes = (ushort) EnabledChanges;
            stream.Serialize(ref changes);
            EnabledChanges = (Changes) changes;

            stream.Serialize(ref Level);
            stream.Serialize(ref StarsToWin);
            stream.Serialize(ref CoinsForPowerup);
            stream.Serialize(ref Lives);
            stream.Serialize(ref TimerSeconds);
            stream.Serialize(ref TeamsEnabled);
            stream.Serialize(ref CustomPowerupsEnabled);
            stream.Serialize(ref DrawOnTimeUp);
        }

        public enum Changes : ushort {
            Level = 1 << 0,
            StarsToWin = 1 << 1,
            CoinsForPowerup = 1 << 2,
            Lives = 1 << 3,
            TimerSeconds = 1 << 4,
            TeamsEnabled = 1 << 5,
            CustomPowerupsEnabled = 1 << 6,
            DrawOnTimeUp = 1 << 7,
        }
    }
}