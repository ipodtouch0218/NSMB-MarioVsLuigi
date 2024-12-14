using NSMB.Translation;
using NSMB.Utils;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using static NSMB.Utils.NetworkUtils;
using NSMB.Extensions;
using Quantum;
using System.Text;

namespace NSMB.UI.MainMenu {
    public class RoomIcon : MonoBehaviour {

        //---Properties
        public bool HasGameStarted {
            get {
                GetCustomProperty(room.CustomProperties, Enums.NetRoomProperties.BoolProperties, out int packedBoolProperties);
                BooleanProperties boolProperties = (BooleanProperties) packedBoolProperties;

                return boolProperties.GameStarted;
            }
        }

        //---Public Variables
        public RoomInfo room;

        //---Serialized Variables
        [SerializeField] private Color defaultColor, selectedColor;
        [SerializeField] private TMP_Text playersText, nameText, inProgressText, symbolsText, mapText;
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
            nameText.text = tm.GetTranslationWithReplacements("ui.rooms.listing.name", "playername", hostname.ToValidUsername(null, PlayerRef.None, false));

            GetCustomProperty(room.CustomProperties, Enums.NetRoomProperties.StageGuid, out string stageAssetGuid);
            GetCustomProperty(room.CustomProperties, Enums.NetRoomProperties.IntProperties, out int packedIntProperties);
            GetCustomProperty(room.CustomProperties, Enums.NetRoomProperties.BoolProperties, out int packedBoolProperties);

            IntegerProperties intProperties = (IntegerProperties) packedIntProperties;
            BooleanProperties boolProperties = (BooleanProperties) packedBoolProperties;

            playersText.text = tm.GetTranslationWithReplacements("ui.rooms.listing.players", "players", room.PlayerCount.ToString(), "maxplayers", room.MaxPlayers.ToString());
            inProgressText.text = boolProperties.GameStarted ? tm.GetTranslation("ui.rooms.listing.status.started") : tm.GetTranslation("ui.rooms.listing.status.notstarted");

            StringBuilder symbols = new();

            if (boolProperties.CustomPowerups) {
                symbols.Append("<sprite name=room_powerups>");
            }

            if (boolProperties.Teams) {
                symbols.Append("<sprite name=room_teams>");
            }

            if (intProperties.Timer > 0) {
                symbols.Append("<sprite name=room_timer>").Append(Utils.Utils.GetSymbolString(intProperties.Timer.ToString(), Utils.Utils.smallSymbols));
            }

            if (intProperties.Lives > 0) {
                symbols.Append("<sprite name=room_lives>").Append(Utils.Utils.GetSymbolString(intProperties.Lives.ToString(), Utils.Utils.smallSymbols));
            }

            symbols.Append("<sprite name=room_stars>").Append(Utils.Utils.GetSymbolString(intProperties.StarRequirement.ToString(), Utils.Utils.smallSymbols));
            symbols.Append("<sprite name=room_coins>").Append(Utils.Utils.GetSymbolString(intProperties.CoinRequirement.ToString(), Utils.Utils.smallSymbols));
            symbolsText.text = symbols.ToString();

            string stageName;
            if (AssetGuid.TryParse(stageAssetGuid, out AssetGuid guid, false)
                && QuantumUnityDB.TryGetGlobalAsset(new AssetRef<Map>(guid), out Map map)
                && QuantumUnityDB.TryGetGlobalAsset(map.UserAsset, out VersusStageData stage)) {

                stageName = tm.GetTranslation(stage.TranslationKey);
            } else {
                stageName = "???";
            }
            mapText.text = tm.GetTranslation("ui.rooms.listing.map");
        }

        public void Select() {
            icon.color = selectedColor;
        }

        public void Unselect() {
            icon.color = defaultColor;
        }
    }
}
