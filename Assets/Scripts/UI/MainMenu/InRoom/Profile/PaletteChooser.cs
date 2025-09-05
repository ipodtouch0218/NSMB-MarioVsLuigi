using NSMB.UI.Elements;
using Quantum;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;
using Navigation = UnityEngine.UI.Navigation;

namespace NSMB.UI.MainMenu.Submenus.InRoom {
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
        private readonly List<Navigation> navigations = new();
        private GameObject blocker;
        private CharacterAsset character;
        private int selectedPaletteIndex;
        private bool initialized;

        public void OnDisable() {
            Close(false);
        }

        public unsafe void Initialize() {
            if (initialized) {
                return;
            }

            int palettesPerRow = template.transform.parent.GetComponent<GridLayoutGroup>().constraintCount;

            PaletteSet[] palettes = GlobalController.Instance.config.Palettes
                .Select(QuantumUnityDB.GetGlobalAsset)
                .OrderBy(ps => ps ? ps.order : int.MinValue)
                .ToArray();

            for (int i = 0; i < palettes.Length; i++) {
                PaletteSet palette = palettes[i];

                GameObject newButton = Instantiate(template, template.transform.parent);
                PaletteButton cb = newButton.GetComponent<PaletteButton>();
                paletteButtons.Add(cb);
                cb.palette = palette;

                Button b = newButton.GetComponent<Button>();
                newButton.name = palette ? palette.name : "Reset";
                if (!palette) {
                    b.image.sprite = clearSprite;
                }

                newButton.SetActive(true);

                Navigation navigation = new() { mode = Navigation.Mode.Explicit };

                if (i > 0 && i % palettesPerRow != 0) {
                    Navigation n = navigations[i - 1];
                    n.selectOnRight = b;
                    navigations[i - 1] = n;
                    navigation.selectOnLeft = paletteButtons[i - 1].button;
                }
                if (i >= palettesPerRow) {
                    Navigation n = navigations[i - palettesPerRow];
                    n.selectOnDown = b;
                    navigations[i - palettesPerRow] = n;
                    navigation.selectOnUp = paletteButtons[i - palettesPerRow].button;
                }

                navigations.Add(navigation);
            }

            for (int i = 0; i < paletteButtons.Count; i++) {
                paletteButtons[i].button.navigation = navigations[i];
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
            ChangePaletteButton(selectedPaletteIndex);
        }

        public void ChangePaletteButton(int index) {
            selectedPaletteIndex = index;
            AssetRef<PaletteSet>[] palettes = GlobalController.Instance.config.Palettes;
            PaletteSet palette = null;

            if (index >= 0 && index < palettes.Length) {
                palette = QuantumUnityDB.GetGlobalAsset(palettes[index]);
            }

            if (palette) {
                overallsImage.enabled = true;
                overallsImage.color = palette.GetPaletteForCharacter(character).OverallsColor.AsColor;
                shirtImage.enabled = true;
                shirtImage.color = palette.GetPaletteForCharacter(character).ShirtColor.AsColor;
                baseImage.sprite = baseSprite;
            } else {
                overallsImage.enabled = false;
                shirtImage.enabled = false;
                baseImage.sprite = clearSprite;
            }
        }

        public void SelectPalette(Button button) {
            PaletteButton selectedButton = paletteButtons.FirstOrDefault(pb => pb.button == button);
            if (selectedButton == null) {
                return;
            }

            QuantumGame game = QuantumRunner.DefaultGame;
            int newIndex = game.Configurations.Simulation.Palettes.IndexOf(pbar => pbar == selectedButton.palette);

            foreach (var slot in game.GetLocalPlayerSlots()) {
                game.SendCommand(slot, new CommandChangePlayerData { 
                    EnabledChanges = CommandChangePlayerData.Changes.Palette,
                    Palette = (byte) newIndex,
                });
            }
            
            Close(false);
            selectedPaletteIndex = newIndex;
            ChangePaletteButton(selectedPaletteIndex);
            Settings.Instance.generalPalette = selectedPaletteIndex;
            Settings.Instance.SaveSettings();
            canvas.PlayConfirmSound();
        }

        public void Open() {
            Initialize();

            blocker = Instantiate(blockerTemplate, canvas.transform);
            blocker.SetActive(true);
            content.SetActive(true);
            canvas.PlayCursorSound();

            QuantumGame game = QuantumRunner.DefaultGame;
            var selectedPalette = game.Configurations.Simulation.Palettes[selectedPaletteIndex];
            var selectedPaletteButton = paletteButtons.FirstOrDefault(pb => pb.palette == selectedPalette);

            if (selectedPaletteButton == null) {
                selectedPaletteButton = paletteButtons[0];
            }

            EventSystem.current.SetSelectedGameObject(selectedPaletteButton.gameObject);
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
