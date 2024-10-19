using NSMB.Translation;
using NSMB.Utils;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using static NSMB.Utils.NetworkUtils;
using NSMB.Extensions;

namespace NSMB.UI.MainMenu {
    public class RoomIcon : MonoBehaviour {

        //---Public Variables
        public RoomInfo room;

        //---Serialized Variables
        [SerializeField] private Color defaultColor, highlightColor, selectedColor;
        [SerializeField] private TMP_Text playersText, nameText, inProgressText, symbolsText;
        [SerializeField] private Image icon;

        public void OnValidate() {
            this.SetIfNull(ref icon);
        }

        public void Start() {
            Unselect();
        }

        public void UpdateUI(RoomInfo newRoomInfo) {
            room = newRoomInfo;

            TranslationManager tm = GlobalController.Instance.translationManager;

            GetCustomProperty(room.CustomProperties, Enums.NetRoomProperties.HostName, out string hostname);
            nameText.text = tm.GetTranslationWithReplacements("ui.rooms.listing.name", "playername", hostname.ToValidUsername());

            GetCustomProperty(room.CustomProperties, Enums.NetRoomProperties.IntProperties, out int packedIntProperties);
            GetCustomProperty(room.CustomProperties, Enums.NetRoomProperties.BoolProperties, out int packedBoolProperties);

            IntegerProperties intProperties = (IntegerProperties) packedIntProperties;
            BooleanProperties boolProperties = (BooleanProperties) packedBoolProperties;

            playersText.text = tm.GetTranslationWithReplacements("ui.rooms.listing.players", "players", room.PlayerCount.ToString(), "maxplayers", room.MaxPlayers.ToString());
             inProgressText.text = boolProperties.GameStarted ? tm.GetTranslation("ui.rooms.listing.status.started") : tm.GetTranslation("ui.rooms.listing.status.notstarted");

            string symbols = "";

            if (boolProperties.CustomPowerups) {
                symbols += "<sprite name=room_powerups>";
            }

            if (boolProperties.Teams) {
                symbols += "<sprite name=room_teams>";
            }

            if (intProperties.Timer > 0) {
                symbols += "<sprite name=room_timer>" + Utils.Utils.GetSymbolString(intProperties.Timer.ToString(), Utils.Utils.smallSymbols);
            }

            if (intProperties.Lives > 0) {
                symbols += "<sprite name=room_lives>" + Utils.Utils.GetSymbolString(intProperties.Lives.ToString(), Utils.Utils.smallSymbols);
            }

            symbols += "<sprite name=room_stars>" + Utils.Utils.GetSymbolString(intProperties.StarRequirement.ToString(), Utils.Utils.smallSymbols);
            symbols += "<sprite name=room_coins>" + Utils.Utils.GetSymbolString(intProperties.CoinRequirement.ToString(), Utils.Utils.smallSymbols);

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
            if (MainMenuManager.Instance.roomManager.SelectedRoomIcon == this) {
                Select();
            } else {
                Unselect();
            }
        }
    }
}
