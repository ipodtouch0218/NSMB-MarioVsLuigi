using Quantum;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu.Submenus.Prompts {
    public class StageGameSettingsPanel : GameSettingsPanel {
        public unsafe override void OnEnable() {
            var stage = QuantumUnityDB.GetGlobalAsset(QuantumRunner.DefaultGame.Frames.Predicted.Global->Rules.Stage).UserAsset;
            var buttons = root.GetComponentsInChildren<StageSelectionButton>(true);
            var randomizeToggle = root.GetComponentInChildren<Toggle>(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform) transform);
            Canvas.ForceUpdateCanvases();
            submenu.Canvas.EventSystem.SetSelectedGameObject(randomizeToggle.gameObject);
        }
    }
}
