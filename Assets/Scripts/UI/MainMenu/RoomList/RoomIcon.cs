using NSMB.UI.Translation;
using NSMB.Utilities;
using NSMB.Utilities.Extensions;
using static NSMB.Utilities.NetworkUtils;
using Photon.Realtime;
using Quantum;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu.Submenus.RoomList {
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
        [SerializeField] private TMP_Text playersText, nameText, inProgressText, symbolsText, mapText;
        [SerializeField] private Image icon;

        public void OnValidate() {
            this.SetIfNull(ref icon);
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

            playersText.text = $"{room.PlayerCount}/{room.MaxPlayers}";
            inProgressText.text = boolProperties.GameStarted ? tm.GetTranslation("ui.rooms.listing.status.started") : tm.GetTranslation("ui.rooms.listing.status.notstarted");

            StringBuilder symbols = new();

            if (boolProperties.CustomPowerups) {
                symbols.Append("<sprite name=room_powerups>");
            }

            if (boolProperties.Teams) {
                symbols.Append("<sprite name=room_teams>");
            }

            if (intProperties.Timer > 0) {
                symbols.Append("<sprite name=room_timer>").Append(Utils.GetSymbolString(intProperties.Timer.ToString(), Utils.smallSymbols));
            }

            if (intProperties.Lives > 0) {
                symbols.Append("<sprite name=room_lives>").Append(Utils.GetSymbolString(intProperties.Lives.ToString(), Utils.smallSymbols));
            }

            symbols.Append("<sprite name=room_stars>").Append(Utils.GetSymbolString(intProperties.StarRequirement.ToString(), Utils.smallSymbols));
            symbols.Append("<sprite name=room_coins>").Append(Utils.GetSymbolString(intProperties.CoinRequirement.ToString(), Utils.smallSymbols));
            symbolsText.text = symbols.ToString();

            string stageName;
            if (stageAssetGuid != null && AssetGuid.TryParse(stageAssetGuid, out AssetGuid guid, true)
                && QuantumUnityDB.TryGetGlobalAsset(new AssetRef<Map>(guid), out Map map)
                && QuantumUnityDB.TryGetGlobalAsset(map.UserAsset, out VersusStageData stage)) {

                stageName = tm.GetTranslation(stage.TranslationKey);
            } else {
                stageName = "???";
            }
            mapText.text = tm.GetTranslationWithReplacements("ui.rooms.listing.map", "map", stageName);
        }
    }
}
