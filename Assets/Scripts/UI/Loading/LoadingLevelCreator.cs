using UnityEngine;
using TMPro;
using Quantum;

namespace NSMB.UI.Loading {

    public class LoadingLevelCreator : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private TMP_Text text;
        [SerializeField] private string key = "ui.loading.levelcreator";
        [SerializeField] private FieldType type;

        public void OnEnable() {
            string value = GetValueFromField();
            if (string.IsNullOrEmpty(value)) {
                text.text = "";
                return;
            }

            // No need to worry about language changes in this state...
            // or else...?
            text.text = GlobalController.Instance.translationManager.GetTranslationWithReplacements(key, "username", value);
        }

        private string GetValueFromField() {
            QuantumGame game = QuantumRunner.DefaultGame;
            if (game == null) {
                return "";
            }

            Frame f = game.Frames.Predicted;
            if (f == null || !f.TryFindAsset(f.Map.UserAsset, out VersusStageData stage) || !stage.ShowAuthorAndComposer) {
                return "";
            }

            return type switch {
                FieldType.Author => stage.StageAuthor,
                FieldType.Composer => stage.MusicComposer,
                _ => ""
            };
        }

        public enum FieldType {
            Author,
            Composer,
        }
    }
}
