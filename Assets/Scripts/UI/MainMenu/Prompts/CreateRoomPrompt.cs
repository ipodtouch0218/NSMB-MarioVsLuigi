using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NSMB.UI.MainMenu;

namespace NSMB.UI.Prompts {
    public class CreateRoomPrompt : UIPrompt {

        //---Serialized Variables
        [SerializeField] private TMP_Text maxPlayersLabel;
        [SerializeField] private Slider maxPlayersSlider;
        [SerializeField] private Toggle privateRoomToggle;

        protected override void SetDefaults() {
            maxPlayersSlider.value = 10;
            privateRoomToggle.SetIsOnWithoutNotify(false);
        }

        public void OnPlayerSliderValueChanged() {
            maxPlayersLabel.text = maxPlayersSlider.value.ToString();
        }

        public async void CreateRoom() {
            byte maxPlayers = (byte) maxPlayersSlider.value;
            gameObject.SetActive(false);

            short result = await NetworkHandler.CreateRoom(new EnterRoomArgs() {
                RoomOptions = new RoomOptions() {
                    MaxPlayers = maxPlayers,
                    IsVisible = !privateRoomToggle.isOn,
                    IsOpen = true,
                },
            });
            if (result != 0) {
                MainMenuManager.Instance.OpenErrorBox(result);
            }
        }
    }
}
