using NSMB.Addons;
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
        [SerializeField] private TMP_Text addonsText;

        //---Private Variables
        private bool creating, createdSuccessfully;
        private bool visible = true;

        public override void Initialize() {
            base.Initialize();
            QuantumCallback.Subscribe<CallbackLocalPlayerAddConfirmed>(this, OnLocalPlayerAddConfirmed);
        }

        public override void Show(bool first) {
            base.Show(first);

            creating = false;
            createdSuccessfully = false;
            if (first) {
                // Default values
                maxPlayerSlider.value = 10;
                MaxPlayerSliderChanged();
                privateToggle.isOn = false;
            }

            int addons = GlobalController.Instance.addonManager.LoadedAddons.Count;
            addonsText.text = GlobalController.Instance.translationManager.GetTranslationWithReplacements(
                addons == 0 ? "ui.rooms.create.addons.notenabled" : "ui.rooms.create.addons.enabled",
                "addons", addons.ToString());
        }

        public override bool TryGoBack(out bool playSound) {
            if (creating) {
                playSound = false;
                return true;
            }

            return base.TryGoBack(out playSound);
        }

        [Preserve]
        public async void ConfirmClicked() {
            creating = true;
            Canvas.PlayConfirmSound();
            visible = !privateToggle.isOn;
            Canvas.GoBack();
            short response = await NetworkHandler.CreateRoom(new Photon.Realtime.EnterRoomArgs {
                RoomOptions = new Photon.Realtime.RoomOptions {
                    MaxPlayers = (int) maxPlayerSlider.value,
                    IsVisible = false,
                }
            });
            createdSuccessfully = (response == 0);
        }

        public void MaxPlayerSliderChanged() {
            sliderValue.text = ((int) maxPlayerSlider.value).ToString();
        }

        private unsafe void OnLocalPlayerAddConfirmed(CallbackLocalPlayerAddConfirmed e) {
            if (createdSuccessfully) {
                NetworkHandler.Client.CurrentRoom.IsVisible = visible;
            }
            creating = false;
            createdSuccessfully = false;
        }

        public class AddonOption : TMP_Dropdown.OptionData {
            public AddonDefinition definition;

            public AddonOption(AddonDefinition def) {
                definition = def;
                text = definition.FullName;
            }
        }
    }
}