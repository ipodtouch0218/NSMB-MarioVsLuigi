using UnityEngine;
using UnityEngine.UI;
using TMPro;

using NSMB.Utils;
using Fusion;

public class RoomIcon : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private Color defaultColor, highlightColor, selectedColor;
    [SerializeField] private TMP_Text playersText, nameText, inProgressText, symbolsText;

    //---Public Variables
    public SessionInfo session;
    public bool joinPrivate;

    //---Components
    private Image icon;

    public void Start() {
        icon = GetComponent<Image>();
        Unselect();
    }

    public void UpdateUI(SessionInfo newSession) {
        if (joinPrivate)
            return;

        session = newSession;

        Utils.GetSessionProperty(session, Enums.NetRoomProperties.HostName, out string hostname);
        Utils.GetSessionProperty(session, Enums.NetRoomProperties.StarRequirement, out int stars);
        Utils.GetSessionProperty(session, Enums.NetRoomProperties.CoinRequirement, out int coins);
        Utils.GetSessionProperty(session, Enums.NetRoomProperties.Lives, out int lives);
        Utils.GetSessionProperty(session, Enums.NetRoomProperties.Time, out int timer);
        Utils.GetSessionProperty(session, Enums.NetRoomProperties.CustomPowerups, out bool powerups);
        Utils.GetSessionProperty(session, Enums.NetRoomProperties.GameStarted, out bool gameStarted);

        nameText.text = hostname.ToValidUsername() + "'s Lobby";
        playersText.text = $"Players: {session.PlayerCount}/{session.MaxPlayers}";
        inProgressText.text = gameStarted ? "In Progress" : "Not Started";

        string symbols = "";
        bool time = timer >= 1;
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
