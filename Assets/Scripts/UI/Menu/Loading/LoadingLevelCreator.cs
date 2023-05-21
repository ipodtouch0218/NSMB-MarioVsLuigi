using UnityEngine;
using TMPro;

using NSMB.Game;

namespace NSMB.Loading {

    public class LoadingLevelCreator : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private TMP_Text text;

        public void OnEnable() {
            if (string.IsNullOrEmpty(GameManager.Instance.levelDesigner)) {
                text.text = "";
                return;
            }

            text.text = GlobalController.Instance.translationManager.GetTranslation("ui.loading.levelcreator").Trim() + " " + GameManager.Instance.levelDesigner;
        }
    }
}
