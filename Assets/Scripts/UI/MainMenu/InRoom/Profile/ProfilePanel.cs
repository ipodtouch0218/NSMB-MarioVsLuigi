using NSMB.Networking;
using NSMB.UI.Elements;
using Quantum;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu.Submenus.InRoom {
    public class ProfilePanel : InRoomSubmenuPanel {

        //---Properties
        public override bool IsInSubmenu => teamChooser.content.activeSelf || paletteChooser.content.activeSelf;

        //---Serialized Variables
        [SerializeField] private Image[] characterButtonImages, characterButtonLogos;
        [SerializeField] private Sprite[] enabledCharacterButtonSprites, disabledCharacterButtonSprites;
        [SerializeField] private Color enabledCharacterButtonLogoColor, disabledCharacterButtonLogoColor;
        [SerializeField] private Image paletteBackground;
        [SerializeField] private PaletteChooser paletteChooser;
        [SerializeField] private TeamChooser teamChooser;
        [SerializeField] private SpriteChangingToggle spectateToggle;

        //---Private Variables
        private int currentCharacterIndex;

        public override void Initialize() {
            paletteChooser.Initialize();
            teamChooser.Initialize();

            QuantumEvent.Subscribe<EventPlayerDataChanged>(this, OnPlayerDataChanged);
        }

        public override bool TryGoBack(out bool playSound) {
            if (teamChooser.content.activeSelf) {
                teamChooser.Close(true);
                playSound = false;
                return false;
            }

            if (paletteChooser.content.activeSelf) {
                paletteChooser.Close(true);
                playSound = false;
                return false;
            }

            return base.TryGoBack(out playSound);
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
                image.sprite = disabledCharacterButtonSprites[i];

                if (i < characterButtonLogos.Length && characterButtonLogos[i]) {
                    characterButtonLogos[i].color = disabledCharacterButtonLogoColor;
                }
            }

            characterButtonImages[index].sprite = enabledCharacterButtonSprites[index];
            paletteBackground.sprite = disabledCharacterButtonSprites[index];
            if (index < characterButtonLogos.Length && characterButtonLogos[index]) {
                characterButtonLogos[index].color = enabledCharacterButtonLogoColor;
            }

            var allCharacters = f.SimulationConfig.CharacterDatas;
            CharacterAsset characterAsset = f.FindAsset(allCharacters[Mathf.Clamp(index, 0, allCharacters.Length)]);
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

            Frame f = e.Game.Frames.Predicted;

            // Set character button to the correct state
            PlayerData* data = QuantumUtils.GetPlayerData(f, e.Player);
            SetPaletteButtonState(data->Palette);
            SetCharacterButtonState(f, data->Character, false);
            spectateToggle.SetIsOnWithoutNotify(data->ManualSpectator);
        }
    }
}
