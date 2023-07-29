using UnityEngine;
using TMPro;

using NSMB.Extensions;
using NSMB.UI.MainMenu;

namespace NSMB.UI.Prompts {
    public class ErrorPrompt : UIPrompt {

        //---Serailized Variables
        [SerializeField] protected TMP_Text errorText;

        public void OpenWithText(string key) {
            if (!gameObject.activeSelf && MainMenuManager.Instance)
                MainMenuManager.Instance.sfx.PlayOneShot(Enums.Sounds.UI_Error);

            gameObject.SetActive(true);
            errorText.text = GlobalController.Instance.translationManager.GetTranslation(key);
        }

        public void Close() {
            if (MainMenuManager.Instance)
                MainMenuManager.Instance.BackSound();

            gameObject.SetActive(false);
        }
    }
}