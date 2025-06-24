using NSMB.UI.Translation;
using NSMB.Utilities;
using Photon.Realtime;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace NSMB.UI.MainMenu.Submenus.Prompts {
    public class ErrorPromptSubmenu : PromptSubmenu {

        //---Serialized Variables
        [SerializeField] private MainMenuSubmenu openSubmenuOnDisconnect;
        [SerializeField] private TMP_Text headerText, errorText;

        public void OpenWithRealtimeErrorCode(short code) {
            OpenWithString(NetworkUtils.RealtimeErrorCodes.GetValueOrDefault(code, "ui.error.unknown"), true);
        }

        public void OpenWithRealtimeDisconnectCause(DisconnectCause cause) {

        }

        public void OpenWithString(string str, bool disconnect) {
            if (disconnect && openSubmenuOnDisconnect) {
                Canvas.OpenMenu(openSubmenuOnDisconnect);
            }

            TranslationManager tm = GlobalController.Instance.translationManager;
            errorText.text = tm.GetTranslation(str);
            Canvas.OpenMenu(this, SoundEffect.UI_Error);
        }
    }
}