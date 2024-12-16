using NSMB.UI.MainMenu;
using Photon.Realtime;
using System.Linq;
using TMPro;
using UnityEngine;

namespace NSMB.UI.Prompts {
    public class JoinPrivateRoomPrompt : UIPrompt {

        //---Serialized Variables
        [SerializeField] private TMP_InputField roomIdInput;

        protected override void SetDefaults() {
            roomIdInput.text = "";
        }

        public async void JoinPrivateRoom() {
            string id = roomIdInput.text.ToUpper();
            int index = id.Length > 0 ? NetworkHandler.RoomIdValidChars.IndexOf(id[0]) : -1;
            if (id.Length < 8 || index < 0 || index >= NetworkHandler.Regions.Count()) {
                MainMenuManager.Instance.OpenErrorBox("ui.rooms.joinprivate.invalid");
                return;
            }

            gameObject.SetActive(false);
            short result = await NetworkHandler.JoinRoom(new EnterRoomArgs() {
                RoomName = id,
            });
            if (result != 0) {
                MainMenuManager.Instance.OpenErrorBox(result);
            }
        }
    }
}
