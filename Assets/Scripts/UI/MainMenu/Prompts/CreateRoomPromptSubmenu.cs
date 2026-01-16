using NSMB.Addons;
using NSMB.Networking;
using Quantum;
using TMPro;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.UI;
using Navigation = UnityEngine.UI.Navigation;

namespace NSMB.UI.MainMenu.Submenus.Prompts {
    public class CreateRoomPromptSubmenu : PromptSubmenu {

        //---Serialized Variables
        [SerializeField] private TMP_Text sliderValue;
        [SerializeField] private Slider maxPlayerSlider;
        [SerializeField] private Toggle privateToggle;
        [SerializeField] private TMP_Text addonsText;
        [SerializeField] private GameObject addonsOption;
        [SerializeField] private Selectable addonsButton, confirmButton;

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

            bool addonsEnabled = GlobalController.Instance.addonManager.isActiveAndEnabled;
            if (addonsEnabled) {
                int addons = GlobalController.Instance.addonManager.LoadedAddons.Count;
                addonsText.text = GlobalController.Instance.translationManager.GetTranslationWithReplacements(
                    addons == 0 ? "ui.rooms.create.addons.notenabled" : "ui.rooms.create.addons.enabled",
                    "addons", addons.ToString());
            }
            addonsOption.SetActive(addonsEnabled);

            // private toggle
            var nav = privateToggle.navigation;
            nav.selectOnDown = addonsEnabled ? addonsButton : confirmButton;
            privateToggle.navigation = nav;

            // confirm button
            nav = confirmButton.navigation;
            nav.selectOnUp = addonsEnabled ? addonsButton : privateToggle;
            confirmButton.navigation = nav;

            // back button
            Selectable backSelectable = backButton.GetComponent<Selectable>();
            nav = backSelectable.navigation;
            nav.selectOnUp = addonsEnabled ? addonsButton : privateToggle;
            backSelectable.navigation = nav;
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