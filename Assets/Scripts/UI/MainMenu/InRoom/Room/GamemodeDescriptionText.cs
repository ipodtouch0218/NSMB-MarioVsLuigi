using NSMB.UI.Translation;
using NSMB.Utilities.Extensions;
using Quantum;
using TMPro;
using UnityEngine;

namespace NSMB.UI.MainMenu.Submenus.InRoom {
    public class GamemodeDescriptionText : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private TMP_Text text;

        public void OnValidate() {
            this.SetIfNull(ref text);
        }

        public void OnEnable() {
            TranslationManager.OnLanguageChanged += OnLanguageChanged;
            OnLanguageChanged(GlobalController.Instance.translationManager);
        }

        public void OnDisable() {
            TranslationManager.OnLanguageChanged += OnLanguageChanged;
        }

        private unsafe void OnLanguageChanged(TranslationManager tm) {
            var game = QuantumRunner.DefaultGame;
            if (game == null) {
                return;
            }

            Frame f = game.Frames.Predicted;
            if (f.TryFindAsset(f.Global->Rules.Gamemode, out var gamemode)) {
                text.text = tm.GetTranslation(gamemode.DescriptionTranslationKey);
            } else {
                text.text = "???";
            }
        }
    }
}