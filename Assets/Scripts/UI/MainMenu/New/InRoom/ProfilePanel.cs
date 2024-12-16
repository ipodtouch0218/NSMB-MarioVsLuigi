using Quantum;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu.Submenus {
    public class ProfilePanel : InRoomSubmenuPanel {

        //---Serialized Variables
        [SerializeField] private Image[] characterButtonImages, characterButtonLogos;
        [SerializeField] private Sprite enabledCharacterButtonSprite, disabledCharacterButtonSprite;
        [SerializeField] private Color enabledCharacterButtonLogoColor, disabledCharacterButtonLogoColor;
        [SerializeField] private PaletteChooser paletteChooser;
        [SerializeField] private SpriteChangingToggle spectateToggle;

        //---Private Variables
        private int currentCharacterIndex;

        public override void Initialize() {
            QuantumEvent.Subscribe<EventPlayerDataChanged>(this, OnPlayerDataChanged);
        }

        public void OnCharacterClicked(int index) {
            var game = NetworkHandler.Runner.Game;
            foreach (int slot in game.GetLocalPlayerSlots()) {
                game.SendCommand(slot, new CommandChangePlayerData {
                    EnabledChanges = CommandChangePlayerData.Changes.Character,
                    Character = (byte) index,
                });
            }
            SetCharacterButtonState(game.Frames.Predicted, index, true);
        }

        public void OnCharacterToggled() {
            OnCharacterClicked((currentCharacterIndex + 1) % characterButtonImages.Length);
        }

        public void OnSpectateToggled() {
            QuantumGame game = NetworkHandler.Runner.Game;
            foreach (var slot in game.GetLocalPlayerSlots()) {
                game.SendCommand(slot, new CommandChangePlayerData {
                    EnabledChanges = CommandChangePlayerData.Changes.Spectating,
                    Spectating = spectateToggle.isOn,
                });
            }
            menu.Canvas.PlayConfirmSound();
        }

        private void SetCharacterButtonState(Frame f, int index, bool sound) {
            bool changed = currentCharacterIndex != index;
            currentCharacterIndex = index;

            for (int i = 0; i < characterButtonImages.Length; i++) {
                var image = characterButtonImages[i];
                image.sprite = disabledCharacterButtonSprite;
                image.transform.SetAsLastSibling();

                if (i < characterButtonLogos.Length && characterButtonLogos[i]) {
                    characterButtonLogos[i].color = disabledCharacterButtonLogoColor;
                }
            }

            characterButtonImages[index].sprite = enabledCharacterButtonSprite;
            characterButtonImages[index].transform.SetAsLastSibling();
            if (index < characterButtonLogos.Length && characterButtonLogos[index]) {
                characterButtonLogos[index].color = enabledCharacterButtonLogoColor;
            }

            SimulationConfig config = f.SimulationConfig;
            CharacterAsset characterAsset = config.CharacterDatas[Mathf.Clamp(index, 0, config.CharacterDatas.Length)];
            paletteChooser.ChangeCharacter(characterAsset);

            if (changed) {
                Settings.Instance.generalCharacter = index;
                Settings.Instance.SaveSettings();
            }

            if (sound && changed) {
                menu.Canvas.PlaySound(SoundEffect.Player_Voice_Selected, characterAsset);
            }
        }

        private void SetPaletteButtonState(int index) {
            paletteChooser.ChangePaletteButton(index);
        }

        //---Callbacks
        private unsafe void OnPlayerDataChanged(EventPlayerDataChanged e) {
            if (!e.Game.PlayerIsLocal(e.Player)) {
                return;
            }

            // Set character button to the correct state
            PlayerData* data = QuantumUtils.GetPlayerData(e.Frame, e.Player);
            SetPaletteButtonState(data->Palette);
            SetCharacterButtonState(e.Frame, data->Character, false);
            spectateToggle.SetIsOnWithoutNotify(data->ManualSpectator);
        }
    }
}
