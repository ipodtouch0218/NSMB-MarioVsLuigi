using NSMB.UI.Elements;
using Quantum;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;
using Navigation = UnityEngine.UI.Navigation;
using Photon.Deterministic;

namespace NSMB.UI.MainMenu.Submenus.InRoom {
    public class PaletteChooser : MonoBehaviour, KeepChildInFocus.IFocusIgnore {

        //---Serialized Variables
        // [SerializeField] private SimulationConfig config;
        [SerializeField] private MainMenuCanvas canvas;
        //[SerializeField] private GameObject template, blockerTemplate;
        [SerializeField] public GameObject content;
        //[SerializeField] private Sprite clearSprite, baseSprite;
        [SerializeField] private CharacterAsset defaultCharacter;
        [SerializeField] private GameObject selectOnClose;
        [SerializeField] private Gradient hueMap, grayscaleMap;
        [SerializeField] private Image sliderButton, sliderButton2, marioPrimary, marioSecondary, luigiPrimary, luigiSecondary, sliderImage1, sliderImage2;
        [SerializeField] private Slider slider, slider2;
        [SerializeField] private bool isPrimarySliderGray, isSecondarySliderGray, isPrimaryHueEnabled, isSecondaryHueEnabled;
        [SerializeField] private Sprite hueMapSprite, grayscaleMapSprite;

        [SerializeField] private Image overallsImage, shirtImage, baseImage;

        //---Private Variables
        private readonly List<PaletteButton> paletteButtons = new();
        private readonly List<Button> buttons = new();
        private readonly List<Navigation> navigations = new();
        private GameObject blocker;
        private CharacterAsset character;
        private int selected;
        private bool initialized;

        public void Initialize() {
            initialized = true;
            slider.value = Settings.Instance.primaryHue;
            slider2.value = Settings.Instance.secondaryHue;
            isPrimaryHueEnabled = Settings.Instance.primaryHueEnabled;
            isSecondaryHueEnabled = Settings.Instance.secondaryHueEnabled;
            isPrimarySliderGray = Settings.Instance.primaryHueGrayscale;
            isSecondarySliderGray = Settings.Instance.secondaryHueGrayscale;
        }
        public void ChangePaletteButton(int index) {
        }


        public void OnDisable() {
            Close(false);
        }

        public void ChangeCharacter(CharacterAsset data) {
            foreach (PaletteButton b in paletteButtons) {
                b.Instantiate(data);
            }
            character = data;
        }

        public void MakeSliderGray(bool firstSlider) {
            if (firstSlider) {
                isPrimarySliderGray = !isPrimarySliderGray;
                sliderImage1.sprite = (isPrimarySliderGray ? grayscaleMapSprite : hueMapSprite);
                ChangeColorOfPrimarySliderButton();
            } else {
                isSecondarySliderGray = !isSecondarySliderGray;
                sliderImage2.sprite = (isPrimarySliderGray ? grayscaleMapSprite : hueMapSprite);
                ChangeColorOfSecondarySliderButton();
            }
            QuantumGame game = QuantumRunner.DefaultGame;
                foreach (var slot in game.GetLocalPlayerSlots()) {
                    game.SendCommand(slot, new CommandChangePlayerData {
                        EnabledChanges = CommandChangePlayerData.Changes.HueSettings,
                        HueSettings = (byte)((isPrimaryHueEnabled ? 1 : 0) + (isPrimarySliderGray ? 2 : 0) + (isSecondaryHueEnabled ? 4 : 0) + (isSecondarySliderGray ? 8 : 0)),
                    });
                }

            Settings.Instance.primaryHueGrayscale = isPrimarySliderGray;
            Settings.Instance.secondaryHueGrayscale = isSecondarySliderGray;
            Settings.Instance.SaveSettings();
        }

        public void ToggleSliderState(bool firstSlider) {
            if (firstSlider) {
                isPrimaryHueEnabled = !isPrimaryHueEnabled;
                ChangeColorOfPrimarySliderButton();
            } else {
                isSecondaryHueEnabled = !isSecondaryHueEnabled;
                ChangeColorOfSecondarySliderButton();
            }
            QuantumGame game = QuantumRunner.DefaultGame;
            foreach (var slot in game.GetLocalPlayerSlots()) {
                game.SendCommand(slot, new CommandChangePlayerData {
                    EnabledChanges = CommandChangePlayerData.Changes.HueSettings,
                    HueSettings = (byte)((isPrimaryHueEnabled ? 1 : 0) + (isPrimarySliderGray ? 2 : 0) + (isSecondaryHueEnabled ? 4 : 0) + (isSecondarySliderGray ? 8 : 0)),
                });
            }

            Settings.Instance.primaryHueEnabled = isPrimaryHueEnabled;
            Settings.Instance.secondaryHueEnabled = isSecondaryHueEnabled;
            Settings.Instance.SaveSettings();
        }


        public void ChangeColorOfPrimarySliderButton() {
            var currentGradient = isPrimarySliderGray ? grayscaleMap : hueMap;
            sliderButton.color = currentGradient.Evaluate(slider.value) * (isPrimaryHueEnabled ? Color.white : new Color(0.35f,0.35f,0.35f,1f));
            marioPrimary.color = currentGradient.Evaluate(slider.value) * (isPrimaryHueEnabled ? Color.white : Color.clear);
            luigiSecondary.color = currentGradient.Evaluate(slider.value) * (isPrimaryHueEnabled ? Color.white : Color.clear);
            sliderImage1.color = (isPrimaryHueEnabled ? Color.white : new Color(0.35f, 0.35f, 0.35f, 1f));
        }

        public void ChangeColorOfSecondarySliderButton() {
            var currentGradient = isSecondarySliderGray ? grayscaleMap : hueMap;
            sliderButton2.color = currentGradient.Evaluate(slider2.value) * (isSecondaryHueEnabled ? Color.white : new Color(0.35f, 0.35f, 0.35f, 1f));
            marioSecondary.color = currentGradient.Evaluate(slider2.value) * (isSecondaryHueEnabled ? Color.white : Color.clear);
            luigiPrimary.color = currentGradient.Evaluate(slider2.value) * (isSecondaryHueEnabled ? Color.white : Color.clear);
            sliderImage2.color = (isSecondaryHueEnabled ? Color.white : new Color(0.35f, 0.35f, 0.35f, 1f));
        }

        public void SelectPrimaryHue() {
            FP newIndex = FP.FromString(slider.value.ToString());
            QuantumGame game = QuantumRunner.DefaultGame;
            foreach (var slot in game.GetLocalPlayerSlots()) {
                game.SendCommand(slot, new CommandChangePlayerData {
                    EnabledChanges = CommandChangePlayerData.Changes.PrimaryHue,
                    PrimaryHue = newIndex.AsFloat,
                });
            }

            Settings.Instance.primaryHue = slider.value;
            Settings.Instance.SaveSettings();
        }

        public void SelectSecondaryHue() {
            FP newIndex = FP.FromString(slider2.value.ToString());
            QuantumGame game = QuantumRunner.DefaultGame;
            foreach (var slot in game.GetLocalPlayerSlots()) {
                game.SendCommand(slot, new CommandChangePlayerData {
                    EnabledChanges = CommandChangePlayerData.Changes.SecondaryHue,
                    SecondaryHue = newIndex.AsFloat,
                });
            }

            Settings.Instance.secondaryHue = slider2.value;
            Settings.Instance.SaveSettings();
        }

        public void Open() {
            content.SetActive(!content.activeInHierarchy);
            canvas.PlaySound(content.activeInHierarchy ? SoundEffect.UI_WindowOpen : SoundEffect.UI_WindowClose);

            //EventSystem.current.SetSelectedGameObject(buttons[selected].gameObject);
        }

        public void Close(bool playSound) {
            Destroy(blocker);
            EventSystem.current.SetSelectedGameObject(selectOnClose);
            content.SetActive(false);

            if (playSound) {
                canvas.PlaySound(SoundEffect.UI_Back);
            }
        }
    }
}
