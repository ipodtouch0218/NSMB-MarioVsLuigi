using Photon.Deterministic;

namespace Quantum {
    public class CommandChangeRules : DeterministicCommand, ILobbyCommand {

        public Rules EnabledChanges;

        public AssetRef<Map> Level;
        public int StarsToWin;
        public int CoinsForPowerup;
        public int Lives;
        public int TimerSeconds;
        public bool TeamsEnabled;
        public bool CustomPowerupsEnabled;
        public bool DrawOnTimeUp;

        public override void Serialize(BitStream stream) {
            ushort changes = (ushort) EnabledChanges;
            stream.Serialize(ref changes);
            EnabledChanges = (Rules) changes;

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

            Rules rulesChanges = EnabledChanges;
            var rules = f.Global->Rules;
            bool levelChanged = false;

            if (rulesChanges.HasFlag(Rules.Level)) {
                levelChanged = rules.Level != Level;
                rules.Level = Level;
            }
            if (rulesChanges.HasFlag(Rules.StarsToWin)) {
                rules.StarsToWin = StarsToWin;
            }
            if (rulesChanges.HasFlag(Rules.CoinsForPowerup)) {
                rules.CoinsForPowerup = CoinsForPowerup;
            }
            if (rulesChanges.HasFlag(Rules.Lives)) {
                rules.Lives = Lives;
            }
            if (rulesChanges.HasFlag(Rules.TimerSeconds)) {
                rules.TimerSeconds = TimerSeconds;
            }
            if (rulesChanges.HasFlag(Rules.TeamsEnabled)) {
                rules.TeamsEnabled = TeamsEnabled;
            }
            if (rulesChanges.HasFlag(Rules.CustomPowerupsEnabled)) {
                rules.CustomPowerupsEnabled = CustomPowerupsEnabled;
            }
            if (rulesChanges.HasFlag(Rules.DrawOnTimeUp)) {
                rules.DrawOnTimeUp = DrawOnTimeUp;
            }

            f.Global->Rules = rules;
            f.Events.RulesChanged(f, levelChanged);

            if (f.Global->GameStartFrames > 0 && !QuantumUtils.IsGameStartable(f)) {
                GameLogicSystem.StopCountdown(f);
            }
        }

        public enum Rules : ushort {
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