using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    public void CreateRoom() {
        byte maxPlayers = (byte) maxPlayersSlider.value;

        _ = NetworkHandler.CreateRoom(new() {
            IsVisible = !privateRoomToggle.isOn
        }, players: maxPlayers);

        gameObject.SetActive(false);
    }
}
