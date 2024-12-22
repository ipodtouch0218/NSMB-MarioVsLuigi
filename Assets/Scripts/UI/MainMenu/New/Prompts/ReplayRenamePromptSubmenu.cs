using System.IO;
using TMPro;
using UnityEngine;
using static ReplayListManager;

namespace NSMB.UI.MainMenu.Submenus.Prompts {
    public class ReplayRenamePromptSubmenu : PromptSubmenu {

        //---Serialized Variables
        [SerializeField] private ReplayListManager manager;
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private TMP_Text defaultValue;

        //---Private Variables
        private Replay target;
        private bool success;

        public void Open(Replay replay) {
            target = replay;
            Canvas.OpenMenu(this);
        }

        public override void Show(bool first) {
            base.Show(first);
            success = false;
            inputField.text = target.ReplayFile.GetDisplayName(false);
            defaultValue.text = target.ReplayFile.GetDefaultName();
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
            if (string.IsNullOrWhiteSpace(inputField.text)) {
                inputField.text = null;
            }

            if (inputField.text != target.ReplayFile.GetDisplayName()) {
                // Confirm + no change = no change. Do this because translations matter.
                target.ReplayFile.CustomName = inputField.text;

                using FileStream file = new FileStream(target.FilePath, FileMode.OpenOrCreate);
                target.ReplayFile.WriteToStream(file);
                target.ListEntry.UpdateText();
            }

            target = null;
            success = true;
            Canvas.GoBack();
        }
    }
}
