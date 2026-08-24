using NSMB.UI.Elements;
using NSMB.Utilities;
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
        [SerializeField] private MainMenuCanvas canvas;
        [SerializeField] private GameObject template, blockerTemplate;
        [SerializeField] public GameObject content;
        [SerializeField] private Sprite clearSprite, baseSprite, randomSprite;
        [SerializeField] private CharacterAsset defaultCharacter;
        [SerializeField] private GameObject selectOnClose;

        [SerializeField] private Image overallsImage, shirtImage, baseImage;

        //---Private Variables
        private readonly List<PaletteButton> paletteButtons = new();
        private GameObject blocker;
        private AssetRef<CharacterAsset> character;
        private AssetRef<PaletteSet> selectedPalette;

        public void OnDisable() {
            Close(false);
        }

        public unsafe void Initialize() {
            foreach (var pb in paletteButtons) {
                Destroy(pb.gameObject);
            }
            paletteButtons.Clear();

            var palettes = AssetRepository<PaletteSet>.AllAssets;

            int palettesPerRow = Mathf.Max(4, palettes.Count / 7);
            template.transform.parent.GetComponent<GridLayoutGroup>().constraintCount = palettesPerRow;

            for (int i = -2; i < palettes.Count; i++) {
                PaletteSet palette = (i >= 0) ? palettes[i] : null; // Add one null entry

                GameObject newButton = Instantiate(template, template.transform.parent);
                PaletteButton cb = newButton.GetComponent<PaletteButton>();
                paletteButtons.Add(cb);
                cb.palette = palette;

                Button b = newButton.GetComponent<Button>();
                
                if (!palette) {
                    if (i == -2)
                    {
                        b.image.sprite = clearSprite;
                        newButton.name = "Reset";
                    }
                    else
                    {
                        b.image.sprite = randomSprite;
                        newButton.name = "Random";
                    }
                }
                else
                {
                    newButton.name = palette.name;
                }

                    newButton.SetActive(true);
            }

            for (int i = 0; i < paletteButtons.Count; i++) {
                Selectable button = paletteButtons[i].button;
                Navigation nav = button.navigation;
                nav.mode = Navigation.Mode.Explicit;

                if (i % palettesPerRow != palettesPerRow - 1) {
                    nav.selectOnRight = Utils.IndexIntoOrDefault(paletteButtons, i + 1, null)?.button;
                }
                if (i % palettesPerRow != 0) {
                    nav.selectOnLeft = Utils.IndexIntoOrDefault(paletteButtons, i - 1, null)?.button;
                }
                nav.selectOnUp = Utils.IndexIntoOrDefault(paletteButtons, i - palettesPerRow, null)?.button;
                nav.selectOnDown = Utils.IndexIntoOrDefault(paletteButtons, i + palettesPerRow, null)?.button;

                button.navigation = nav;
            }

            foreach (PaletteButton b in paletteButtons) {
                b.Instantiate(character);
            }
        }

        public void ChangeCharacter(AssetRef<CharacterAsset> data) {
            foreach (PaletteButton b in paletteButtons) {
                b.Instantiate(data);
            }
            character = data;
            ChangePaletteButton(selectedPalette);
        }

        public void ChangePaletteButton(AssetRef<PaletteSet> paletteRef) {
            selectedPalette = paletteRef;

            if (QuantumUnityDB.TryGetGlobalAsset(paletteRef, out var palette)) {
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

            if (button.gameObject.name == "Random")
            {
                int randomindex = Random.Range(2, paletteButtons.Count);
                selectedButton = paletteButtons[randomindex];
            }
            QuantumGame game = QuantumRunner.DefaultGame;
            foreach (var slot in game.GetLocalPlayerSlots()) {
                game.SendCommand(slot, new CommandChangePlayerData { 
                    EnabledChanges = CommandChangePlayerData.Changes.Palette,
                    Palette = selectedButton.palette,
                });
            }
            
            Close(false);
            selectedPalette = selectedButton.palette;
            ChangePaletteButton(selectedPalette);
            Settings.Instance.generalPalette = selectedPalette;
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
