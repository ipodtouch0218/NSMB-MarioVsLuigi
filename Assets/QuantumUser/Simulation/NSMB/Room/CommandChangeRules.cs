using Photon.Deterministic;
using System;

namespace Quantum {
    public class CommandChangeRules : DeterministicCommand, ILobbyCommand {

        public Rules EnabledChanges;

        public AssetRef<Map> Stage;
        public AssetRef<GamemodeAsset> Gamemode;
        public int StarsToWin;
        public int CoinsForPowerup;
        public int Lives;
        public int TimerMinutes;
        public bool TeamsEnabled;
        public bool CustomPowerupsEnabled;
        public bool DrawOnTimeUp;

        public override void Serialize(BitStream stream) {
            ushort changes = (ushort) EnabledChanges;
            stream.Serialize(ref changes);
            EnabledChanges = (Rules) changes;

            stream.Serialize(ref Stage);
            stream.Serialize(ref Gamemode);
            stream.Serialize(ref StarsToWin);
            stream.Serialize(ref CoinsForPowerup);
            stream.Serialize(ref Lives);
            stream.Serialize(ref TimerMinutes);
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
            bool gamemodeChanged = false;
            bool levelChanged = false;

            if (rulesChanges.HasFlag(Rules.Gamemode)) {
                gamemodeChanged = rules.Gamemode != Gamemode;

                GameRules tempRules = default;
                f.FindAsset(Gamemode).DefaultRules.Materialize(f, ref tempRules);
                tempRules.Stage = rules.Stage;

                rules = tempRules;
            }
            if (rulesChanges.HasFlag(Rules.Stage)) {
                levelChanged = rules.Stage != Stage;
                rules.Stage = Stage;
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
            if (rulesChanges.HasFlag(Rules.TimerMinutes)) {
                rules.TimerMinutes = TimerMinutes;
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
            f.Events.RulesChanged(gamemodeChanged, levelChanged);

            if (f.Global->GameStartFrames > 0 && !QuantumUtils.IsGameStartable(f)) {
                GameLogicSystem.StopCountdown(f);
            }
        }

        [Flags]
        public enum Rules : ushort {
            None = 0,
            Stage = 1 << 0,
            Gamemode = 1 << 1,
            StarsToWin = 1 << 2,
            CoinsForPowerup = 1 << 3,
            Lives = 1 << 4,
            TimerMinutes = 1 << 5,
            TeamsEnabled = 1 << 6,
            CustomPowerupsEnabled = 1 << 7,
            DrawOnTimeUp = 1 << 8,
        }
    }
}