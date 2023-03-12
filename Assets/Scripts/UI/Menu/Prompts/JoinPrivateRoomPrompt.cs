using UnityEngine;
using TMPro;

public class JoinPrivateRoomPrompt : UIPrompt {

    //---Serialized Variables
    [SerializeField] private TMP_InputField roomIdInput;

    protected override void SetDefaults() {
        roomIdInput.text = "";
    }

    public void JoinPrivateRoom() {
        string id = roomIdInput.text.ToUpper();
        int index = id.Length > 0 ? NetworkHandler.RoomIdValidChars.IndexOf(id[0]) : -1;
        if (id.Length < 8 || index < 0 || index >= NetworkHandler.Regions.Length) {
            MainMenuManager.Instance.OpenErrorBox("Invalid Room ID");
            return;
        }

        gameObject.SetActive(false);
        _ = NetworkHandler.JoinRoom(id);
    }
}
