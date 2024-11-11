using Photon.Deterministic;

namespace Quantum {
    public class CommandChangeRules : DeterministicCommand, ILobbyCommand {

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

        public unsafe void Execute(Frame f, PlayerRef sender, PlayerData* playerData) {
            if (f.Global->GameState != GameState.PreGameRoom || !playerData->IsRoomHost) {
                // Only the host can change rules.
                return;
            }

            Changes rulesChanges = EnabledChanges;
            var rules = f.Global->Rules;
            bool levelChanged = false;

            if (rulesChanges.HasFlag(Changes.Level)) {
                levelChanged = rules.Level != Level;
                rules.Level = Level;
            }
            if (rulesChanges.HasFlag(Changes.StarsToWin)) {
                rules.StarsToWin = StarsToWin;
            }
            if (rulesChanges.HasFlag(Changes.CoinsForPowerup)) {
                rules.CoinsForPowerup = CoinsForPowerup;
            }
            if (rulesChanges.HasFlag(Changes.Lives)) {
                rules.Lives = Lives;
            }
            if (rulesChanges.HasFlag(Changes.TimerSeconds)) {
                rules.TimerSeconds = TimerSeconds;
            }
            if (rulesChanges.HasFlag(Changes.TeamsEnabled)) {
                rules.TeamsEnabled = TeamsEnabled;
            }
            if (rulesChanges.HasFlag(Changes.CustomPowerupsEnabled)) {
                rules.CustomPowerupsEnabled = CustomPowerupsEnabled;
            }
            if (rulesChanges.HasFlag(Changes.DrawOnTimeUp)) {
                rules.DrawOnTimeUp = DrawOnTimeUp;
            }

            f.Global->Rules = rules;
            f.Events.RulesChanged(f, levelChanged);

            if (f.Global->GameStartFrames > 0 && !QuantumUtils.IsGameStartable(f)) {
                GameLogicSystem.StopCountdown(f);
            }
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