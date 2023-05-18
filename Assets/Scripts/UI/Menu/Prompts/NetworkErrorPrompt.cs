using UnityEngine;
using TMPro;

using NSMB.Extensions;

namespace NSMB.UI.Prompts {
    public class NetworkErrorPrompt : UIPrompt {

        //---Serailized Variables
        [SerializeField] private TMP_Text errorText;

        public void OpenWithText(string key) {
            if (!gameObject.activeSelf && MainMenuManager.Instance)
                MainMenuManager.Instance.sfx.PlayOneShot(Enums.Sounds.UI_Error);

            gameObject.SetActive(true);
            errorText.text = GlobalController.Instance.translationManager.GetTranslation(key);
        }

        public void Reconnect() {
            _ = NetworkHandler.ConnectToSameRegion();
            gameObject.SetActive(false);
        }

        public void Close() {
            if (MainMenuManager.Instance)
                MainMenuManager.Instance.BackSound();

            gameObject.SetActive(false);
        }
    }
}
