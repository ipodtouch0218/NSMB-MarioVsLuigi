using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu.Submenus.Prompts {
    public class CreateRoomPromptSubmenu : PromptSubmenu {

        //---Serialized Variables
        [SerializeField] private TMP_Text sliderValue;
        [SerializeField] private Slider maxPlayerSlider;
        [SerializeField] private Toggle privateToggle;

        //---Private Variables
        private bool success;

        public override void Show(bool first) {
            base.Show(first);

            if (first) {
                // Default values
                maxPlayerSlider.value = 10;
                MaxPlayerSliderChanged();
                privateToggle.isOn = false;
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
            success = true;
            Canvas.PlayConfirmSound();
            _ = NetworkHandler.CreateRoom(new Photon.Realtime.EnterRoomArgs {
                RoomOptions = new Photon.Realtime.RoomOptions {
                    MaxPlayers = (int) maxPlayerSlider.value,
                    IsVisible = !privateToggle.isOn,
                }
            });
            Canvas.GoBack();
        }

        public void MaxPlayerSliderChanged() {
            sliderValue.text = ((int) maxPlayerSlider.value).ToString();
        }
    }
}