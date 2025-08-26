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
        [SerializeField] private Gradient hueMap;
        [SerializeField] private Image sliderButton, sliderButton2, marioPrimary, marioSecondary, luigiPrimary, luigiSecondary;
        [SerializeField] private Slider slider, slider2;

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

        public void ChangeColorOfSliderButton(bool isSecondary) {
            if (isSecondary) {
                sliderButton2.color = hueMap.Evaluate(slider2.value);
                marioSecondary.color = hueMap.Evaluate(slider2.value);
                luigiPrimary.color = hueMap.Evaluate(slider2.value);
            } else {
                sliderButton.color = hueMap.Evaluate(slider.value);
                marioPrimary.color = hueMap.Evaluate(slider.value);
                luigiSecondary.color = hueMap.Evaluate(slider.value);
            }
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
