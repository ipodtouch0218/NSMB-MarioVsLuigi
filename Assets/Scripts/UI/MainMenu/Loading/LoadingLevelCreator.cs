using UnityEngine;
using TMPro;
using Quantum;

namespace NSMB.Loading {

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

            text.text = GlobalController.Instance.translationManager.GetTranslationWithReplacements(key, "username", value);
        }

        private string GetValueFromField() {
            Frame f = QuantumRunner.DefaultGame.Frames.Predicted;
            if (f == null || !f.TryFindAsset(f.Map.UserAsset, out VersusStageData stage)) {
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
