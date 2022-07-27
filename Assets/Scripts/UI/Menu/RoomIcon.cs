﻿using UnityEngine;
using UnityEngine.UI;
using Photon.Realtime;
using TMPro;
using NSMB.Utils;

public class RoomIcon : MonoBehaviour {

    private Image icon;
    public RoomInfo room;
    public bool joinPrivate;
    public Color defaultColor, highlightColor, selectedColor;

    [SerializeField] private TMP_Text playersText, nameText, inProgressText, symbolsText;

    public void Start() {
        icon = GetComponent<Image>();
        Unselect();
    }

    public void UpdateUI(RoomInfo newRoom) {
        if (joinPrivate)
            return;

        room = newRoom;
        ExitGames.Client.Photon.Hashtable prop = room.CustomProperties;

        nameText.text = $"{prop[Enums.NetRoomProperties.HostName.Filter()]}'s Lobby";
        playersText.text = $"Players: {room.PlayerCount}/{room.MaxPlayers}";
        inProgressText.text = (bool) prop[Enums.NetRoomProperties.GameStarted] ? "In Progress" : "Not Started";

        string symbols = "";
        Utils.GetCustomProperty(Enums.NetRoomProperties.StarRequirement, out int stars, newRoom.CustomProperties);
        Utils.GetCustomProperty(Enums.NetRoomProperties.CoinRequirement, out int coins, newRoom.CustomProperties);
        Utils.GetCustomProperty(Enums.NetRoomProperties.Lives, out int lives, newRoom.CustomProperties);
        bool powerups = (bool) prop[Enums.NetRoomProperties.NewPowerups];
        bool time = ((int) prop[Enums.NetRoomProperties.Time]) >= 1;
        //bool password = ((string) prop[Enums.NetRoomProperties.Password]) != "";

        if (powerups)
            symbols += "<sprite=8>";
        if (time)
            symbols += "<sprite=6>";

        if (lives >= 1)
            symbols += "<sprite=9>" + Utils.GetSymbolString(lives.ToString(), Utils.smallSymbols);

        symbols += "<sprite=38>" + Utils.GetSymbolString(stars.ToString(), Utils.smallSymbols);
        symbols += "<sprite=37>" + Utils.GetSymbolString(coins.ToString(), Utils.smallSymbols);
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
