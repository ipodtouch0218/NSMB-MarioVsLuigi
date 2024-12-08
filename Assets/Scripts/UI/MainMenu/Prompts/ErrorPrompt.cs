using NSMB.Extensions;
using NSMB.UI.MainMenu;
using TMPro;
using UnityEngine;

namespace NSMB.UI.Prompts {
    public class ErrorPrompt : UIPrompt {

        //---Serailized Variables
        [SerializeField] protected TMP_Text errorText;

        public void OpenWithText(string key, params string[] replacements) {
            if (!gameObject.activeSelf && MainMenuManager.Instance) {
                MainMenuManager.Instance.sfx.PlayOneShot(SoundEffect.UI_Error);
            }

            gameObject.SetActive(true);
            errorText.text = GlobalController.Instance.translationManager.GetTranslationWithReplacements(key, replacements);
        }

        public void Close() {
            if (MainMenuManager.Instance) {
                MainMenuManager.Instance.BackSound();
            }

            gameObject.SetActive(false);
        }
    }
}