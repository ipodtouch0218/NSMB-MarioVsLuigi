using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class NetworkManager : MonoBehaviourPunCallbacks {
    public static string PROPKEY_MAP = "level", PROPKEY_STARS = "stars";
    public static NetworkManager instance;

    void Awake() {
        if (instance != null && instance != this) {
            this.gameObject.SetActive(false);
            return;
        }
        instance = this;
        DontDestroyOnLoad(this.gameObject);
    }

    public override void OnConnectedToMaster() {
        Debug.Log("Connected to Master");
        PhotonNetwork.JoinLobby();
    }

    public override void OnCreatedRoom() {
        Debug.Log("Created Room: " + PhotonNetwork.CurrentRoom.Name);

        ExitGames.Client.Photon.Hashtable table = new ExitGames.Client.Photon.Hashtable();
        table[NetworkManager.PROPKEY_MAP] = 0;
        table[NetworkManager.PROPKEY_STARS] = 10;
        PhotonNetwork.CurrentRoom.SetCustomProperties(table);
    }

    public override void OnJoinedRoom() {
        MainMenuManager mmm = MainMenuManager.Instance;
        if (mmm) {
            RoomInfo room = PhotonNetwork.CurrentRoom;
            mmm.OpenInLobbyMenu(room);
            mmm.ChatMessage(PhotonNetwork.LocalPlayer.NickName + " joined the lobby", MainMenuManager.ColorToVector(Color.red));
            mmm.levelDropdown.interactable = PhotonNetwork.IsMasterClient;
            mmm.PopulatePlayerList();
            
            if (room.CustomProperties != null) {
                if (room.CustomProperties[PROPKEY_MAP] != null)
                    mmm.SetLevelIndex((int) room.CustomProperties[PROPKEY_MAP]);
                if (room.CustomProperties[PROPKEY_STARS] != null)
                    mmm.SetStarRequirement((int) room.CustomProperties[PROPKEY_STARS]);
            }
        }
    }
    public override void OnPlayerEnteredRoom(Player newPlayer) {
        MainMenuManager mmm = MainMenuManager.Instance;
        if (mmm) {
            mmm.ChatMessage(newPlayer.NickName + " joined the lobby", MainMenuManager.ColorToVector(Color.red));
            mmm.PopulatePlayerList();
        }
    }
    public override void OnPlayerLeftRoom(Player otherPlayer) {
        MainMenuManager mmm = MainMenuManager.Instance;
        if (mmm) {
            mmm.ChatMessage(otherPlayer.NickName + " left the lobby", MainMenuManager.ColorToVector(Color.red));
            mmm.PopulatePlayerList();
        } 
    }

    public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable properties) {
        if (PhotonNetwork.IsMasterClient) {
            return;
        }
        MainMenuManager mmm = MainMenuManager.Instance;
        // base.OnRoomPropertiesUpdate(propertiesThatChanged);
        if (mmm != null && properties != null) {
            if (properties.ContainsKey(PROPKEY_MAP))
                mmm.SetLevelIndex((int) properties[PROPKEY_MAP]);
            if (properties.ContainsKey(PROPKEY_STARS))
                mmm.SetStarRequirement((int) properties[PROPKEY_STARS]);
        }
    }

    public override void OnLeftRoom() {
        
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList) {
        MainMenuManager.Instance.OnRoomListUpdate(roomList);
    }

    public override void OnErrorInfo(ErrorInfo errorInfo) {
        Debug.LogError(errorInfo.Info);
    }
}
