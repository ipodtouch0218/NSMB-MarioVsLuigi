using NSMB.Networking;
using TMPro;
using UnityEngine;

namespace NSMB.UI.MainMenu.Submenus.Prompts {
    public class UpdatePromptSubmenu : PromptSubmenu {

        //---Serialized Variables
        [SerializeField] private TMP_Text text;

        //---Private Variables
        private string remoteVersion;
        private bool upToDate = true, alreadyPrompted, success;

        public override void Initialize() {
            base.Initialize();
            UpdateChecker.IsUpToDate((isUpToDate, newVersion) => {
                remoteVersion = newVersion;
                upToDate = isUpToDate;
            });
        }

        public override void Show(bool first) {
            base.Show(first);
            success = false;
        }

        public override bool TryGoBack(out bool playSound) {
            if (success) {
                playSound = false;
                return true;
            }

            return base.TryGoBack(out playSound);
        }

        public void OpenDownloadsPage() {
            Application.OpenURL("https://github.com/ipodtouch0218/NSMB-MarioVsLuigi/releases/latest");
            Canvas.PlayConfirmSound();
            success = true;
            Canvas.GoBack();
        }

        public void ShowIfNeeded() {
            if (!upToDate && !alreadyPrompted) {
                text.text = GlobalController.Instance.translationManager.GetTranslationWithReplacements("ui.update.prompt",
                    "newversion", remoteVersion,
                    "currentversion", Application.version);
                Canvas.OpenMenu(this);
                alreadyPrompted = true;
            }
        }
    }
}