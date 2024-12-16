using Quantum;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;
using Navigation = UnityEngine.UI.Navigation;

namespace NSMB.UI.MainMenu {
    public class PaletteChooser : MonoBehaviour, KeepChildInFocus.IFocusIgnore {

        //---Serialized Variables
        // [SerializeField] private SimulationConfig config;
        [SerializeField] private MainMenuCanvas canvas;
        [SerializeField] private GameObject template, blockerTemplate;
        [SerializeField] public GameObject content;
        [SerializeField] private Sprite clearSprite, baseSprite;
        [SerializeField] private CharacterAsset defaultCharacter;
        [SerializeField] private GameObject selectOnClose;

        [SerializeField] private Image overallsImage, shirtImage, baseImage;

        //---Private Variables
        private readonly List<PaletteButton> paletteButtons = new();
        private readonly List<Button> buttons = new();
        private readonly List<Navigation> navigations = new();
        private GameObject blocker;
        private CharacterAsset character;
        private int selected;
        private bool initialized;

        public unsafe void Initialize() {
            if (initialized) {
                return;
            }

            PaletteSet[] colors = ScriptableManager.Instance.skins;

            for (int i = 0; i < colors.Length; i++) {
                PaletteSet color = colors[i];

                GameObject newButton = Instantiate(template, template.transform.parent);
                PaletteButton cb = newButton.GetComponent<PaletteButton>();
                paletteButtons.Add(cb);
                cb.palette = color;

                Button b = newButton.GetComponent<Button>();
                newButton.name = color ? color.name : "Reset";
                if (color == null) {
                    b.image.sprite = clearSprite;
                }

                newButton.SetActive(true);
                buttons.Add(b);

                Navigation navigation = new() { mode = Navigation.Mode.Explicit };

                if (i > 0 && i % 4 != 0) {
                    Navigation n = navigations[i - 1];
                    n.selectOnRight = b;
                    navigations[i - 1] = n;
                    navigation.selectOnLeft = buttons[i - 1];
                }
                if (i >= 4) {
                    Navigation n = navigations[i - 4];
                    n.selectOnDown = b;
                    navigations[i - 4] = n;
                    navigation.selectOnUp = buttons[i - 4];
                }

                navigations.Add(navigation);
            }

            for (int i = 0; i < buttons.Count; i++) {
                buttons[i].navigation = navigations[i];
            }
            initialized = true;

            foreach (PaletteButton b in paletteButtons) {
                b.Instantiate(defaultCharacter);
            }
        }

        public void ChangeCharacter(CharacterAsset data) {
            foreach (PaletteButton b in paletteButtons) {
                b.Instantiate(data);
            }
            character = data;
            ChangePaletteButton(selected);
        }

        public void ChangePaletteButton(int index) {
            selected = index;
            PaletteSet[] palettes = ScriptableManager.Instance.skins;
            PaletteSet palette = null;
            if (index >= 0 && index < palettes.Length) {
                palette = palettes[index];
            }

            if (palette) {
                overallsImage.enabled = true;
                overallsImage.color = palette.GetPaletteForCharacter(character).overallsColor;
                shirtImage.enabled = true;
                shirtImage.color = palette.GetPaletteForCharacter(character).shirtColor;
                baseImage.sprite = baseSprite;
            } else {
                overallsImage.enabled = false;
                shirtImage.enabled = false;
                baseImage.sprite = clearSprite;
            }
        }

        public void SelectPalette(Button button) {
            int newIndex = buttons.IndexOf(button);
            QuantumGame game = NetworkHandler.Runner.Game;
            foreach (var slot in game.GetLocalPlayerSlots()) {
                game.SendCommand(slot, new CommandChangePlayerData { 
                    EnabledChanges = CommandChangePlayerData.Changes.Palette,
                    Palette = (byte) newIndex,
                });
            }
            
            Close(false);
            selected = newIndex;
            ChangePaletteButton(selected);
            Settings.Instance.generalPalette = selected;
            Settings.Instance.SaveSettings();
            canvas.PlayConfirmSound();
        }

        public void Open() {
            Initialize();

            blocker = Instantiate(blockerTemplate, canvas.transform);
            blocker.SetActive(true);
            content.SetActive(true);
            canvas.PlayCursorSound();

            EventSystem.current.SetSelectedGameObject(buttons[selected].gameObject);
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
