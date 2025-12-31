using NSMB.Utilities;
using Quantum;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu.Submenus.InRoom {
    public class StageChangeableRule : ChangeableRule {

        //---Properties
        public override bool CanIncreaseValue {
            get {
                QuantumGame game = QuantumRunner.DefaultGame;
                var allStages = QuantumViewUtils.Maps;
                int currentIndex = allStages.IndexOf(map => map == (AssetRef<Map>) value);
                return currentIndex < allStages.Length - 1;
            }
        }
        public override bool CanDecreaseValue {
            get {
                QuantumGame game = QuantumRunner.DefaultGame;
                var allStages = QuantumViewUtils.Maps;
                int currentIndex = allStages.IndexOf(map => map == (AssetRef<Map>) value);
                return currentIndex > 0;
            }
        }

        //---Serialized Variables
        [SerializeField] private Image stagePreview;
        [SerializeField] private Sprite unknownMapSprite;

        protected override void IncreaseValueInternal() {
            QuantumGame game = QuantumRunner.DefaultGame;
            var allStages = QuantumViewUtils.Maps;
            int currentIndex = allStages.IndexOf(map => map == (AssetRef<Map>) value);
            int newIndex = Mathf.Min(currentIndex + 1, allStages.Length - 1);

            if (currentIndex != newIndex) {
                value = allStages[newIndex];
                cursorSfx.Play();
                SendCommand();
            }
        }

        protected override void DecreaseValueInternal() {
            QuantumGame game = QuantumRunner.DefaultGame;
            var allStages = QuantumViewUtils.Maps;
            int currentIndex = allStages.IndexOf(map => map == (AssetRef<Map>) value);
            int newIndex = Mathf.Max(currentIndex - 1, 0);

            if (currentIndex != newIndex) {
                value = allStages[newIndex];
                cursorSfx.Play();
                SendCommand();
            }
        }

        private unsafe void SendCommand() {
            CommandChangeRules cmd = new CommandChangeRules {
                EnabledChanges = ruleType,
            };
            switch (ruleType) {
            case CommandChangeRules.Rules.Stage:
                cmd.Stage = (AssetRef<Map>) value;
                break;
            }

            QuantumGame game = QuantumRunner.DefaultGame;
            PlayerRef host = game.Frames.Predicted.Global->Host;
            if (game.PlayerIsLocal(host)) {
                game.AddCommand(game.GetLocalPlayerSlots()[game.GetLocalPlayers().IndexOf(host)], cmd);
            }
        }

        protected override void UpdateLabel() {
            string stageName;
            Sprite sprite;
            if (value is AssetRef<Map> mapAsset
                && QuantumUnityDB.TryGetGlobalAsset(mapAsset, out Map map)
                && QuantumUnityDB.TryGetGlobalAsset(map.UserAsset, out VersusStageData stage)) {
                stageName = GlobalController.Instance.translationManager.GetTranslation(stage.TranslationKey);
                sprite = stage.Icon;
            } else {
                stageName = "???";
                sprite = unknownMapSprite;
            }
            label.text = labelPrefix + stageName;
            stagePreview.sprite = sprite;
        }
    }
}
