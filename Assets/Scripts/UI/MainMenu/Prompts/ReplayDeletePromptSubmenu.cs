using NSMB.UI.MainMenu.Submenus.Replays;
using System.IO;
using TMPro;
using UnityEngine;

namespace NSMB.UI.MainMenu.Submenus.Prompts {
    public class ReplayDeletePromptSubmenu : PromptSubmenu {

        //---Serialized Variables
        [SerializeField] private ReplayListManager manager;
        [SerializeField] private TMP_Text text;

        //---Private Variables
        private ReplayListEntry target;
        private bool success;

        public void Open(ReplayListEntry replay) {
            target = replay;
            Canvas.OpenMenu(this);
        }

        public override void Show(bool first) {
            base.Show(first);
            success = false;
            text.text = GlobalController.Instance.translationManager.GetTranslationWithReplacements("ui.extras.replays.delete.text", 
                "replayname", target.ReplayFile.Header.GetDisplayName());
        }

        public override bool TryGoBack(out bool playSound) {
            if (success) {
                Canvas.PlayConfirmSound();
                playSound = false;
                return true;
            }

            return base.TryGoBack(out playSound);
        }

        public void ClickConfirm() {
            manager.RemoveReplay(target);
            try {
                File.Delete(target.ReplayFile.FilePath);
            } catch { }
            target = null;
            success = true;
            Canvas.GoBack();
        }
    }
}