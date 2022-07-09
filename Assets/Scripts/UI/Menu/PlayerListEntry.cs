using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Realtime;
using TMPro;

public class PlayerListEntry : MonoBehaviour {

    [SerializeField] TMP_Text nameText, pingText;
    private Image background;

    public void UpdateText(Player player) {

        if (!background)
            background = GetComponent<Image>();
        background.color = Utils.GetPlayerColor(player, 2f, 1f);

        string permissionSymbol = "";
        if (player.IsMasterClient)
            permissionSymbol += "<sprite=5>";

        Utils.GetCustomProperty(Enums.NetPlayerProperties.Status, out bool status, player.CustomProperties);
        if (status)
            permissionSymbol += "<sprite=26>";

        string characterSymbol = Utils.GetCharacterData(player).uistring;
        Utils.GetCustomProperty(Enums.NetPlayerProperties.Ping, out int ping, player.CustomProperties);

        string pingColor;
        if (ping < 0) {
            pingColor = "black";
        } else if (ping < 80) {
            pingColor = "#00b900";
        } else if (ping < 120) {
            pingColor = "orange";
        } else {
            pingColor = "red";
        }

        nameText.text = permissionSymbol + characterSymbol + player.NickName;
        pingText.text = $"<color={pingColor}>{ping}</color>";
    }

}