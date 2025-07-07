using Quantum;
using UnityEngine;

namespace NSMB.UI.MainMenu.Submenus.InRoom {
    public class GamemodeChangeableRule : ChangeableRule {

        //---Properties
        public override bool CanIncreaseValue {
            get {
                QuantumGame game = QuantumRunner.DefaultGame;
                var allGamemodes = game.Configurations.Simulation.AllGamemodes;
                int currentIndex = allGamemodes.IndexOf(gm => gm == (AssetRef<GamemodeAsset>) value);
                return currentIndex < allGamemodes.Length - 1;
            }
        }
        public override bool CanDecreaseValue {
            get {
                QuantumGame game = QuantumRunner.DefaultGame;
                var allGamemodes = game.Configurations.Simulation.AllGamemodes;
                int currentIndex = allGamemodes.IndexOf(gm => gm == (AssetRef<GamemodeAsset>) value);
                return currentIndex > 0;
            }
        }

        protected override void IncreaseValueInternal() {
            QuantumGame game = QuantumRunner.DefaultGame;
            var allGamemodes = game.Configurations.Simulation.AllGamemodes;
            int currentIndex = allGamemodes.IndexOf(gm => gm == (AssetRef<GamemodeAsset>) value);
            int newIndex = Mathf.Min(currentIndex + 1, allGamemodes.Length - 1);

            if (currentIndex != newIndex) {
                value = allGamemodes[newIndex];
                cursorSfx.Play();
                SendCommand();
            }
        }

        protected override void DecreaseValueInternal() {
            QuantumGame game = QuantumRunner.DefaultGame;
            var allGamemodes = game.Configurations.Simulation.AllGamemodes;
            int currentIndex = allGamemodes.IndexOf(gm => gm == (AssetRef<GamemodeAsset>) value);
            int newIndex = Mathf.Max(currentIndex - 1, 0);

            if (currentIndex != newIndex) {
                value = allGamemodes[newIndex];
                cursorSfx.Play();
                SendCommand();
            }
        }

        private unsafe void SendCommand() {
            CommandChangeRules cmd = new CommandChangeRules {
                EnabledChanges = ruleType,
            };

            QuantumGame game = QuantumRunner.DefaultGame;
            switch (ruleType) {
            case CommandChangeRules.Rules.Gamemode:
                cmd.Gamemode = (AssetRef<GamemodeAsset>) value;
                break;
            }

            int slot = game.GetLocalPlayerSlots()[game.GetLocalPlayers().IndexOf(game.Frames.Predicted.Global->Host)];
            game.SendCommand(slot, cmd);
        }

        protected override void UpdateLabel() {
            string stageName;
            if (value is AssetRef<GamemodeAsset> gamemodeAsset
                && QuantumUnityDB.TryGetGlobalAsset(gamemodeAsset, out GamemodeAsset gamemode)) {
                stageName = gamemode.NamePrefix + GlobalController.Instance.translationManager.GetTranslation(gamemode.TranslationKey);
            } else {
                stageName = "???";
            }
            label.text = labelPrefix + stageName;
        }
    }
}
