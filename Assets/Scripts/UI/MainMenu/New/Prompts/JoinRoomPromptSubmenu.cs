using TMPro;
using UnityEngine;

namespace NSMB.UI.MainMenu.Submenus.Prompts {
    public class JoinRoomPromptSubmenu : PromptSubmenu {

        //---Serialized Variables
        [SerializeField] private TMP_InputField idInputField;
        [SerializeField] private TMP_Text invalidText;

        //---Private Variables
        private bool success;

        public override void Show(bool first) {
            base.Show(first);

            if (first) {
                // Default values
                idInputField.text = "";
            }
            success = false;
        }

        public override bool TryGoBack(out bool playSound) {
            if (success) {
                playSound = false;
                return true;
            }

            return base.TryGoBack(out playSound);
        }

        public void ConfirmClicked() {
            if (!NetworkHandler.IsValidRoomId(idInputField.text)) {
                Canvas.PlaySound(SoundEffect.UI_Error);
                return;
            }

            success = true;
            Canvas.PlayConfirmSound();
            _ = NetworkHandler.JoinRoom(new Photon.Realtime.EnterRoomArgs {
                RoomName = idInputField.text
            });
            Canvas.GoBack();
        }

        public void IDTextChanged() {
            invalidText.enabled = !NetworkHandler.IsValidRoomId(idInputField.text);
        }
    }
}