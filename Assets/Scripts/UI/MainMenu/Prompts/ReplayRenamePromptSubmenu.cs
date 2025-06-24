using NSMB.UI.MainMenu.Submenus.Replays;
using System;
using System.IO;
using TMPro;
using UnityEngine;

namespace NSMB.UI.MainMenu.Submenus.Prompts {
    public class ReplayRenamePromptSubmenu : PromptSubmenu {

        //---Serialized Variables
        [SerializeField] private ReplayListManager manager;
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private TMP_Text defaultValue;

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
            inputField.text = target.ReplayFile.Header.GetDisplayName(false);
            defaultValue.text = target.ReplayFile.Header.GetDefaultName();
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

            if (inputField.text != target.ReplayFile.Header.GetDisplayName()) {
                // Confirm + no change = no change. Do this because translations matter.
                try {
                    if (target.ReplayFile.LoadAllIfNeeded() != Replay.ReplayParseResult.Success) {
                        throw new Exception();
                    }
                    target.ReplayFile.Header.CustomName = inputField.text;
                    using FileStream file = new FileStream(target.ReplayFile.FilePath, FileMode.OpenOrCreate);
                    file.SetLength(0);
                    target.ReplayFile.WriteToStream(file);
                } catch (Exception e) {
                    Debug.LogWarning("Failed to save renamed replay: " + e);
                }
                target.UpdateText();
            }

            target = null;
            success = true;
            Canvas.GoBack();
        }
    }
}
