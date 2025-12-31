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
    public class CharacterChooser : MonoBehaviour, KeepChildInFocus.IFocusIgnore {

        //---Serialized Variables
        [SerializeField] private MainMenuCanvas canvas;
        [SerializeField] private GameObject template, blockerTemplate;
        [SerializeField] public GameObject content;
        [SerializeField] private GameObject selectOnClose;
        [SerializeField] private Image button, buttonIcon;
        [SerializeField] private Sprite defaultSelectionSprite;

        //---Private Variables
        private readonly List<CharacterButton> characterButtons = new();
        private readonly List<Navigation> navigations = new();
        private GameObject blocker;
        private AssetRef<CharacterAsset> selectedCharacter;

        public void OnDisable() {
            Close(false);
        }

        public unsafe void Initialize() {
            foreach (var pb in characterButtons) {
                Destroy(pb.gameObject);
            }
            characterButtons.Clear();

            List<CharacterAsset> characters = QuantumViewUtils.Characters
                .OrderBy(ca => ca ? ca.SelectionOrder : int.MinValue)
                .ToList();
            
            int charactersPerRow = Mathf.Max(4, characters.Count / 7);
            template.transform.parent.GetComponent<GridLayoutGroup>().constraintCount = charactersPerRow;

            for (int i = 0; i < characters.Count; i++) {
                var character = characters[i];

                GameObject newButton = Instantiate(template, template.transform.parent);
                CharacterButton cb = newButton.GetComponent<CharacterButton>();
                characterButtons.Add(cb);
                cb.Initialize(character);

                Button b = cb.button;
                newButton.name = character.name;
                newButton.SetActive(true);

                Navigation navigation = new() { mode = Navigation.Mode.Explicit };

                if (i > 0 && i % charactersPerRow != 0) {
                    Navigation n = navigations[i - 1];
                    n.selectOnRight = b;
                    navigations[i - 1] = n;
                    navigation.selectOnLeft = characterButtons[i - 1].button;
                }
                if (i >= charactersPerRow) {
                    Navigation n = navigations[i - charactersPerRow];
                    n.selectOnDown = b;
                    navigations[i - charactersPerRow] = n;
                    navigation.selectOnUp = characterButtons[i - charactersPerRow].button;
                }

                navigations.Add(navigation);
            }

            for (int i = 0; i < characterButtons.Count; i++) {
                characterButtons[i].button.navigation = navigations[i];
            }
        }

        public void ChangeCharacterButton(AssetRef<CharacterAsset> newCharacter) {
            selectedCharacter = newCharacter;

            if (QuantumUnityDB.TryGetGlobalAsset(newCharacter, out var character)) {
                button.color = character.SelectionColor;
                buttonIcon.sprite = character.SelectionSprite;
            } else {
                button.color = Color.red;
                buttonIcon.sprite = defaultSelectionSprite;
            }
        }

        public void SelectCharacter(Button button) {
            var selectedButton = characterButtons.FirstOrDefault(pb => pb.button == button);
            if (selectedButton == null) {
                return;
            }

            QuantumGame game = QuantumRunner.DefaultGame;
            foreach (var slot in game.GetLocalPlayerSlots()) {
                game.AddCommand(slot, new CommandChangePlayerData {
                    EnabledChanges = CommandChangePlayerData.Changes.Character,
                    Character = selectedButton.character,
                });
            }

            Close(false);
            selectedCharacter = selectedButton.character;
            ChangeCharacterButton(selectedCharacter);

            Settings.Instance.generalCharacter = selectedCharacter;
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
            var selectedButton = characterButtons.FirstOrDefault(pb => pb.character == selectedCharacter);
            if (selectedButton == null) {
                selectedButton = characterButtons[0];
            }

            EventSystem.current.SetSelectedGameObject(selectedButton.gameObject);
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
