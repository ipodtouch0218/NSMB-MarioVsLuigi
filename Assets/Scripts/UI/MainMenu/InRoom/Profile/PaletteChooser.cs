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
        [SerializeField] private SpriteChangingToggle enabledButton1, enabledButton2, grayButton1, grayButton2;
//        [SerializeField] private bool grayButton1.isOn, grayButton2.isOn, enabledButton1.isOn, enabledButton2.isOn;
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
            slider.SetValueWithoutNotify(Settings.Instance.primaryHue);
            slider2.SetValueWithoutNotify(Settings.Instance.secondaryHue);
            enabledButton1.SetIsOnWithoutNotify(Settings.Instance.primaryHueEnabled);
            enabledButton2.SetIsOnWithoutNotify(Settings.Instance.secondaryHueEnabled);
            grayButton1.SetIsOnWithoutNotify(Settings.Instance.primaryHueGrayscale);
            grayButton2.SetIsOnWithoutNotify(Settings.Instance.secondaryHueGrayscale);
        }

        public void SetAllTheValues() {

            Initialize();
            
            if (grayButton1.isOn) {
                MakeSliderGray(true);
            }
            if (grayButton2.isOn) {
                MakeSliderGray(false);
            }
            if (enabledButton1.isOn) {
                ToggleSliderState(true);
            }
            if (enabledButton2.isOn) {
                ToggleSliderState(false);
            }
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
                sliderImage1.sprite = (grayButton1.isOn ? grayscaleMapSprite : hueMapSprite);
                ChangeColorOfPrimarySliderButton();
            } else {
                sliderImage2.sprite = (grayButton2.isOn ? grayscaleMapSprite : hueMapSprite);
                ChangeColorOfSecondarySliderButton();
            }
            QuantumGame game = QuantumRunner.DefaultGame;
                foreach (var slot in game.GetLocalPlayerSlots()) {
                    game.SendCommand(slot, new CommandChangePlayerData {
                        EnabledChanges = CommandChangePlayerData.Changes.HueSettings,
                        HueSettings = (byte)((enabledButton1.isOn ? 1 : 0) + (grayButton1.isOn ? 2 : 0) + (enabledButton2.isOn ? 4 : 0) + (grayButton2.isOn ? 8 : 0)),
                    });
                }

            Settings.Instance.primaryHueGrayscale = grayButton1.isOn;
            Settings.Instance.secondaryHueGrayscale = grayButton2.isOn;
            Settings.Instance.SaveSettings();
        }

        public void ToggleSliderState(bool firstSlider) {
            if (firstSlider) {
                slider.enabled = enabledButton1.isOn;
                slider.interactable = enabledButton1.isOn;
                grayButton1.enabled = enabledButton1.isOn;
                grayButton1.interactable = enabledButton1.isOn;
                ChangeColorOfPrimarySliderButton();
            } else {
                slider2.enabled = enabledButton2.isOn;
                slider2.interactable = enabledButton2.isOn;
                grayButton2.enabled = enabledButton2.isOn;
                grayButton2.interactable = enabledButton2.isOn;
                ChangeColorOfSecondarySliderButton();
            }
            QuantumGame game = QuantumRunner.DefaultGame;
            foreach (var slot in game.GetLocalPlayerSlots()) {
                game.SendCommand(slot, new CommandChangePlayerData {
                    EnabledChanges = CommandChangePlayerData.Changes.HueSettings,
                    HueSettings = (byte)((enabledButton1.isOn ? 1 : 0) + (grayButton1.isOn ? 2 : 0) + (enabledButton2.isOn ? 4 : 0) + (grayButton2.isOn ? 8 : 0)),
                });
            }

            Settings.Instance.primaryHueEnabled = enabledButton1.isOn;
            Settings.Instance.secondaryHueEnabled = enabledButton2.isOn;
            Settings.Instance.SaveSettings();
        }


        public void ChangeColorOfPrimarySliderButton() {
            var currentGradient = grayButton1.isOn ? grayscaleMap : hueMap;
            sliderButton.color = currentGradient.Evaluate(slider.value) * (slider.interactable ? Color.white : new Color(0.2f,0.2f,0.2f,1f));
            marioPrimary.color = currentGradient.Evaluate(slider.value) * (slider.interactable ? Color.white : Color.clear);
            luigiSecondary.color = currentGradient.Evaluate(slider.value) * (slider.interactable ? Color.white : Color.clear);
            sliderImage1.color = (slider.interactable ? Color.white : new Color(0.2f, 0.2f, 0.2f, 1f));
        }

        public void ChangeColorOfSecondarySliderButton() {
            var currentGradient = grayButton2.isOn ? grayscaleMap : hueMap;
            sliderButton2.color = currentGradient.Evaluate(slider2.value) * (slider2.interactable ? Color.white : new Color(0.2f, 0.2f, 0.2f, 1f));
            marioSecondary.color = currentGradient.Evaluate(slider2.value) * (slider2.interactable ? Color.white : Color.clear);
            luigiPrimary.color = currentGradient.Evaluate(slider2.value) * (slider2.interactable ? Color.white : Color.clear);
            sliderImage2.color = (slider2.interactable ? Color.white : new Color(0.2f, 0.2f, 0.2f, 1f));
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
            SetAllTheValues();
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
