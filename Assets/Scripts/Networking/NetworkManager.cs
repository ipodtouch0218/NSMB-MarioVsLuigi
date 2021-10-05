using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class NetworkManager : MonoBehaviourPunCallbacks {
    public static string MAP_INDEX_KEY = "level", MAP_STARS = "stars";
    public static NetworkManager instance;

    void Start() {
        PhotonNetwork.ConnectUsingSettings();
    }

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
        // PhotonNetwork.JoinOrCreateRoom("Room", new Photon.Realtime.RoomOptions {MaxPlayers = 64}, Photon.Realtime.TypedLobby.Default);
        PhotonNetwork.JoinLobby();
    }

    public override void OnCreatedRoom() {
        Debug.Log("created room " + PhotonNetwork.CurrentRoom.Name);

        ExitGames.Client.Photon.Hashtable table = new ExitGames.Client.Photon.Hashtable();
        table[NetworkManager.MAP_INDEX_KEY] = 0;
        table[NetworkManager.MAP_STARS] = 15;
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
                if (room.CustomProperties[MAP_INDEX_KEY] != null)
                    mmm.SetLevelIndex((int) room.CustomProperties[MAP_INDEX_KEY]);
                if (room.CustomProperties[MAP_STARS] != null)
                mmm.SetStarRequirement((int) room.CustomProperties[MAP_STARS]);
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
            if (properties.ContainsKey(MAP_INDEX_KEY))
                mmm.SetLevelIndex((int) properties[MAP_INDEX_KEY]);
            if (properties.ContainsKey(MAP_STARS))
                mmm.SetStarRequirement((int) properties[MAP_STARS]);
        }
    }

    public override void OnLeftRoom() {
        
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList) {
        MainMenuManager.Instance.OnRoomListUpdate(roomList);
    }

    public override void OnErrorInfo(ErrorInfo errorInfo) {
        Debug.Log(errorInfo.Info);
    }
}
