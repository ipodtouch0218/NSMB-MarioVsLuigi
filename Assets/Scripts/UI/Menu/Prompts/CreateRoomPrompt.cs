using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NSMB.UI.Prompts {
    public class CreateRoomPrompt : UIPrompt {

        //---Serialized Variables
        [SerializeField] private TMP_Text maxPlayersLabel;
        [SerializeField] private Slider maxPlayersSlider;
        [SerializeField] private Toggle privateRoomToggle;

        protected override void SetDefaults() {
            maxPlayersSlider.value = 10;
            privateRoomToggle.isOn = false;
        }

        public void OnPlayerSliderValueChanged() {
            maxPlayersLabel.text = maxPlayersSlider.value.ToString();
        }

        public async void CreateRoom() {
            byte maxPlayers = (byte) maxPlayersSlider.value;
            gameObject.SetActive(false);

            await NetworkHandler.CreateRoom(new() {
                IsVisible = !privateRoomToggle.isOn
            }, players: maxPlayers);
        }
    }
}
