using Photon.Client;
using Photon.Realtime;
using Quantum;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu.Submenus.Prompts {
    public class RoomSettingsPromptSubmenu : PromptSubmenu, IInRoomCallbacks {

        //---Properties
        public bool RoomIdVisible {
            get => _roomIdVisible;
            set {
                roomIdLabel.text = value ? NetworkHandler.Client.CurrentRoom.Name : GlobalController.Instance.translationManager.GetTranslation("ui.inroom.settings.room.roomid.hidden");
                _roomIdVisible = value;
            }
        }

        //---Serialized Variables
        [SerializeField] private TMP_Text sliderValue, roomIdLabel;
        [SerializeField] private Slider maxPlayerSlider;
        [SerializeField] private Toggle privateToggle;

        //---Private Variables
        private bool _roomIdVisible;

        public override void Show(bool first) {
            base.Show(first);

            Room currentRoom = NetworkHandler.Client.CurrentRoom;
            maxPlayerSlider.value = currentRoom.MaxPlayers;
            MaxPlayerSliderChanged();
            privateToggle.isOn = !currentRoom.IsVisible;
            RoomIdVisible = false;
            NetworkHandler.Client.AddCallbackTarget(this);
        }

        public override void Hide(SubmenuHideReason hideReason) {
            base.Hide(hideReason);
            NetworkHandler.Client.RemoveCallbackTarget(this);
        }

        public unsafe void PrivateToggleChanged() {
            Room currentRoom = NetworkHandler.Client.CurrentRoom;
            QuantumGame game = NetworkHandler.Game;
            PlayerRef host = QuantumUtils.GetHostPlayer(game.Frames.Predicted, out _);
            if (!game.PlayerIsLocal(host)) {
                Canvas.PlaySound(SoundEffect.UI_Error);
                privateToggle.isOn = !currentRoom.IsVisible;
                return;
            }

            currentRoom.IsVisible = !privateToggle.isOn;
            Canvas.PlayCursorSound();
        }

        public unsafe void MaxPlayerSliderChanged() {
            Room currentRoom = NetworkHandler.Client.CurrentRoom;
            QuantumGame game = NetworkHandler.Game;
            PlayerRef host = QuantumUtils.GetHostPlayer(game.Frames.Predicted, out _);
            if (!game.PlayerIsLocal(host)) {
                // Canvas.PlaySound(SoundEffect.UI_Error);
                maxPlayerSlider.SetValueWithoutNotify(currentRoom.MaxPlayers);
                return;
            } else {
                maxPlayerSlider.SetValueWithoutNotify(Mathf.Clamp((int) maxPlayerSlider.value, Mathf.Max(2, currentRoom.PlayerCount), 10));
                currentRoom.MaxPlayers = (int) maxPlayerSlider.value;
            }

            sliderValue.text = ((int) maxPlayerSlider.value).ToString();
        }

        public void RoomIdClicked() {
            RoomIdVisible = !RoomIdVisible;
            Canvas.PlayCursorSound();
        }

        public void OnPlayerEnteredRoom(Player newPlayer) { }

        public void OnPlayerLeftRoom(Player otherPlayer) { }

        public unsafe void OnRoomPropertiesUpdate(PhotonHashtable propertiesThatChanged) {
            Room currentRoom = NetworkHandler.Client.CurrentRoom;
            QuantumGame game = NetworkHandler.Game;
            PlayerRef host = QuantumUtils.GetHostPlayer(game.Frames.Predicted, out _);
            if (!game.PlayerIsLocal(host)) {
                maxPlayerSlider.SetValueWithoutNotify(currentRoom.MaxPlayers);
                privateToggle.SetIsOnWithoutNotify(!currentRoom.IsVisible);
                return;
            }
        }

        public void OnPlayerPropertiesUpdate(Player targetPlayer, PhotonHashtable changedProps) { }

        public void OnMasterClientSwitched(Player newMasterClient) { }
    }
}