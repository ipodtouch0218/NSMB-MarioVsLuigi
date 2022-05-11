using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Realtime;
using TMPro;

public class RoomIcon : MonoBehaviour {
    Image icon;
    public RoomInfo room;
    public bool joinPrivate;
    public Color defaultColor, highlightColor, selectedColor;

    [SerializeField] TMP_Text playersText, nameText, inProgressText, symbolsText;
    void Start() {
        icon = GetComponent<Image>();
        Unselect();
    }
    public void UpdateUI(RoomInfo newRoom) {
        if (joinPrivate)
            return;

        room = newRoom;
        ExitGames.Client.Photon.Hashtable prop = room.CustomProperties;

        nameText.text = $"{prop[Enums.NetRoomProperties.HostName]}'s Lobby";
        playersText.text = $"Players: {room.PlayerCount}/{room.MaxPlayers}";
        inProgressText.text = (bool) prop[Enums.NetRoomProperties.GameStarted] ? "In Progress" : "Not Started";

        string symbols = "";
        bool lives = ((int) prop[Enums.NetRoomProperties.Lives]) >= 1;
        bool powerups = (bool) prop[Enums.NetRoomProperties.NewPowerups];
        bool time = ((int) prop[Enums.NetRoomProperties.Time]) >= 1;
        //bool password = ((string) prop[Enums.NetRoomProperties.Password]) != "";

        if (lives)
            symbols += "<sprite=9>";
        if (powerups)
            symbols += "<sprite=8>";
        if (time)
            symbols += "<sprite=6>";
        //if (password)
        //    symbols += "<sprite=7>";


        symbolsText.text = symbols;
    }
    public void Select() {
        icon.color = selectedColor;
    }
    public void Unselect() {
        icon.color = defaultColor;
    }
    public void Hover() {
        icon.color = highlightColor;
    }
    public void Unhover() {
        if (MainMenuManager.Instance.selectedRoomIcon == this) {
            Select();
        } else {
            Unselect();
        }
    }
}
