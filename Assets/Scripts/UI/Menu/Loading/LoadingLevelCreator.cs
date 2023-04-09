using UnityEngine;
using TMPro;

namespace NSMB.Loading {

    public class LoadingLevelCreator : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private TMP_Text text;

        public void OnEnable() {
            if (string.IsNullOrEmpty(GameManager.Instance.levelDesigner))
                return;

            text.text = GlobalController.Instance.translationManager.GetTranslation("ui.loading.levelcreator").Trim() + " " + GameManager.Instance.levelDesigner;
        }
    }
}
