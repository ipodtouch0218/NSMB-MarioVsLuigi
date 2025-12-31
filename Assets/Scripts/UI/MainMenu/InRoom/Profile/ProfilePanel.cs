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
        [SerializeField] private Image paletteBackground;
        [SerializeField] private CharacterChooser characterChooser;
        [SerializeField] private PaletteChooser paletteChooser;
        [SerializeField] private TeamChooser teamChooser;
        [SerializeField] private SpriteChangingToggle spectateToggle;

        //---Private Variables
        private AssetRef<CharacterAsset> currentCharacter;

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

        public void OnCharacterClicked(AssetRef<CharacterAsset> character) {
            var game = NetworkHandler.Runner.Game;
            foreach (int slot in game.GetLocalPlayerSlots()) {
                game.AddCommand(slot, new CommandChangePlayerData {
                    EnabledChanges = CommandChangePlayerData.Changes.Character,
                    Character = character,
                });
            }
            SetCharacterButtonState(game.Frames.Predicted, character, true);
        }

        public void OnSpectateToggled() {
            QuantumGame game = NetworkHandler.Runner.Game;
            foreach (var slot in game.GetLocalPlayerSlots()) {
                game.AddCommand(slot, new CommandChangePlayerData {
                    EnabledChanges = CommandChangePlayerData.Changes.Spectating,
                    Spectating = spectateToggle.isOn,
                });
            }
            menu.Canvas.PlayConfirmSound();
        }

        private void SetCharacterButtonState(Frame f, AssetRef<CharacterAsset> characterRef, bool sound) {
            bool changed = currentCharacter != characterRef;
            currentCharacter = characterRef;

            characterChooser.ChangeCharacterButton(characterRef);

            if (changed) {
                Settings.Instance.generalCharacter = characterRef;
                Settings.Instance.SaveSettings();
            }

            if (sound && changed && f.TryFindAsset(characterRef, out var character)) {
                menu.Canvas.PlaySound(SoundEffect.Player_Voice_Selected, new[] { character });
            }
        }

        private void SetPaletteButtonState(AssetRef<PaletteSet> palette) {
            paletteChooser.ChangePaletteButton(palette);
        }

        //---Callbacks
        private unsafe void OnPlayerDataChanged(EventPlayerDataChanged e) {
            if (!e.Game.PlayerIsLocal(e.Player)) {
                return;
            }

            Frame f = e.Game.Frames.Predicted;

            // Set character button to the correct state
            PlayerData* data = QuantumUtils.GetPlayerData(f, e.Player);
            SetCharacterButtonState(f, data->Character, false);
            SetPaletteButtonState(data->Palette);
            spectateToggle.SetIsOnWithoutNotify(data->ManualSpectator);
        }
    }
}
