using NSMB.Networking;
using Quantum;
using TMPro;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu.Submenus.Prompts {
    public class CreateRoomPromptSubmenu : PromptSubmenu {

        //---Serialized Variables
        [SerializeField] private TMP_Text sliderValue;
        [SerializeField] private Slider maxPlayerSlider;
        [SerializeField] private Toggle privateToggle;

        //---Private Variables
        private bool success;
        private bool visible = true;

        public override void Initialize() {
            base.Initialize();
            QuantumCallback.Subscribe<CallbackLocalPlayerAddConfirmed>(this, OnLocalPlayerAddConfirmed);
        }

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

        [Preserve]
        public void ConfirmClicked() {
            success = true;
            Canvas.PlayConfirmSound();
            visible = !privateToggle.isOn;
            _ = NetworkHandler.CreateRoom(new Photon.Realtime.EnterRoomArgs {
                RoomOptions = new Photon.Realtime.RoomOptions {
                    MaxPlayers = (int) maxPlayerSlider.value,
                    IsVisible = false,
                }
            });
            Canvas.GoBack();
        }

        public void MaxPlayerSliderChanged() {
            sliderValue.text = ((int) maxPlayerSlider.value).ToString();
        }

        private void OnLocalPlayerAddConfirmed(CallbackLocalPlayerAddConfirmed e) {
            if (success && e.PlayerSlot == 0) {
                NetworkHandler.Client.CurrentRoom.IsVisible = visible;
            }
        }
    }
}