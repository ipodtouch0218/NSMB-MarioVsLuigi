using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Realtime;
using TMPro;

public class RoomIcon : MonoBehaviour {
    Image icon;
    public RoomInfo room;
    public Color defaultColor, highlightColor, selectedColor;

    [SerializeField] TMP_Text playersText, nameText, inProgressText;
    void Start() {
        icon = GetComponent<Image>();
        Unselect();
    }
    public void UpdateUI() {
        ExitGames.Client.Photon.Hashtable prop = room.CustomProperties;

        nameText.text = $"{prop[Enums.NetRoomProperties.HostName]}'s Lobby";
        playersText.text = $"Players: {room.PlayerCount}/{room.MaxPlayers}";
        inProgressText.text = (bool) prop[Enums.NetRoomProperties.GameStarted] ? "In Progress" : "In Lobby";
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
        if (MainMenuManager.Instance.selectedRoom == this) {
            Select();
        } else {
            Unselect();
        }
    }
}
