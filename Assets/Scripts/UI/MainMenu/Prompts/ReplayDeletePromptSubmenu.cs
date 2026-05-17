using NSMB.UI.MainMenu.Submenus.Replays;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Scripting;

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

        [Preserve]
        public async void ClickConfirm() {
            int index = manager.ReplayListEntries.IndexOf(target);

            manager.RemoveReplay(target);
            try {
                File.Delete(target.ReplayFile.FilePath);
            } catch { }
            target = null;
            success = true;
            Canvas.GoBack();

            await manager.CreateReplayListEntries(default);
            if (manager.ReplayListEntries.Count > 0) {
                index = Mathf.Clamp(0, index, manager.ReplayListEntries.Count - 1);
                manager.Select(manager.ReplayListEntries[index], true);
            }
        }
    }
}