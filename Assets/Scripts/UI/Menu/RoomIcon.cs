using UnityEngine;
using UnityEngine.UI;
using TMPro;

using Fusion;
using NSMB.Translation;
using NSMB.Utils;

public class RoomIcon : MonoBehaviour {

    //---Public Variables
    public SessionInfo session;

    //---Serialized Variables
    [SerializeField] private Color defaultColor, highlightColor, selectedColor;
    [SerializeField] private TMP_Text playersText, nameText, inProgressText, symbolsText;

    //---Private Variables
    private Image icon;

    public void Start() {
        icon = GetComponent<Image>();
        Unselect();
    }

    public void UpdateUI(SessionInfo newSession) {
        session = newSession;

        TranslationManager tm = GlobalController.Instance.translationManager;

        NetworkUtils.GetSessionProperty(session, Enums.NetRoomProperties.MaxPlayers, out int maxPlayers);
        NetworkUtils.GetSessionProperty(session, Enums.NetRoomProperties.HostName, out string hostname);
        NetworkUtils.GetSessionProperty(session, Enums.NetRoomProperties.StarRequirement, out int stars);
        NetworkUtils.GetSessionProperty(session, Enums.NetRoomProperties.CoinRequirement, out int coins);
        NetworkUtils.GetSessionProperty(session, Enums.NetRoomProperties.Lives, out int lives);
        NetworkUtils.GetSessionProperty(session, Enums.NetRoomProperties.Time, out int timer);
        NetworkUtils.GetSessionProperty(session, Enums.NetRoomProperties.CustomPowerups, out bool powerups);
        NetworkUtils.GetSessionProperty(session, Enums.NetRoomProperties.GameStarted, out bool gameStarted);
        NetworkUtils.GetSessionProperty(session, Enums.NetRoomProperties.Teams, out bool teams);


        nameText.text = tm.GetTranslationWithReplacements("ui.rooms.listing.name", "playername", hostname.ToValidUsername());
        playersText.text = tm.GetTranslationWithReplacements("ui.rooms.listing.players", "players", session.PlayerCount.ToString(), "maxplayers", maxPlayers.ToString());
        inProgressText.text = gameStarted ? tm.GetTranslation("ui.rooms.listing.status.started") : tm.GetTranslation("ui.rooms.listing.status.notstarted");

        string symbols = "";

        if (powerups)
            symbols += "<sprite name=room_powerups>";
        if (teams)
            symbols += "<sprite name=room_teams>";
        if (timer > 0)
            symbols += "<sprite name=room_timer>" + Utils.GetSymbolString(timer.ToString(), Utils.smallSymbols);

        if (lives > 0)
            symbols += "<sprite name=room_lives>" + Utils.GetSymbolString(lives.ToString(), Utils.smallSymbols);

        symbols += "<sprite name=room_stars>" + Utils.GetSymbolString(stars.ToString(), Utils.smallSymbols);
        symbols += "<sprite name=room_coins>" + Utils.GetSymbolString(coins.ToString(), Utils.smallSymbols);

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
        if (MainMenuManager.Instance.roomManager.SelectedRoom == this) {
            Select();
        } else {
            Unselect();
        }
    }
}
