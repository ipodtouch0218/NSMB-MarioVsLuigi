using System.Reflection;
using UnityEngine;
using TMPro;

using NSMB.Game;

namespace NSMB.Loading {

    public class LoadingLevelCreator : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private TMP_Text text;
        [SerializeField] private string key = "ui.loading.levelcreator", field = "levelDesigner";

        public void OnEnable() {
            string value = GetValueFromField();
            if (string.IsNullOrEmpty(value)) {
                text.text = "";
                return;
            }

            text.text = GlobalController.Instance.translationManager.GetTranslationWithReplacements(key, "username", value);
        }

        private string GetValueFromField() {
            return GameManager.Instance.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance).GetValue(GameManager.Instance) as string;
        }
    }
}
