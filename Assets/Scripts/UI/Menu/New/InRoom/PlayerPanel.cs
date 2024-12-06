using UnityEngine;

namespace NSMB.UI.MainMenu {
    public class PlayerPanel : InRoomPanel {

        //---Properties
        public override GameObject DefaultSelectedObject => playerList.GetPlayerEntryAtIndex(0).gameObject;

        //---Serialized Variables
        [SerializeField] private PlayerListHandler playerList;

    }
}