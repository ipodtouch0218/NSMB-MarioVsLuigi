using NSMB.Translation;
using NSMB.Utils;
using Photon.Realtime;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace NSMB.UI.MainMenu.Submenus.Prompts {
    public class ErrorPromptSubmenu : PromptSubmenu {

        //---Serialized Variables
        [SerializeField] private TMP_Text headerText, errorText;

        public void OpenWithRealtimeErrorCode(short code) {
            TranslationManager tm = GlobalController.Instance.translationManager;
            string key = NetworkUtils.RealtimeErrorCodes.GetValueOrDefault(code, "ui.error.unknown");
            errorText.text = tm.GetTranslation(key);
        }

        public void OpenWithRealtimeDisconnectCause(DisconnectCause cause) {

        }
    }
}