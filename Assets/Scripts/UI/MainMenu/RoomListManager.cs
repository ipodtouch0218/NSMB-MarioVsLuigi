using Photon.Realtime;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NSMB.UI.MainMenu {
    public class RoomListManager : MonoBehaviour, ILobbyCallbacks, IConnectionCallbacks {

        //---Serialized Variables
        [SerializeField] private RoomIcon roomIconPrefab;
        [SerializeField] private GameObject roomListScrollRect, privateRoomIdPrompt;

        //---Private Variables
        private readonly Dictionary<string, RoomIcon> rooms = new();

        public void Start() {
            NetworkHandler.Client.AddCallbackTarget(this);
            roomIconPrefab.gameObject.SetActive(false);
        }

        public void OnDestroy() {
            NetworkHandler.Client?.RemoveCallbackTarget(this);
        }

        public void OpenPrivateRoomPrompt() {
            privateRoomIdPrompt.SetActive(true);
        }

        public void RefreshRooms() {
            foreach (RoomIcon room in rooms.Values) {
                 room.UpdateUI(room.room);
            }
        }

        private void CreateRoom(RoomInfo newRoomInfo) {
            RoomIcon roomIcon = Instantiate(roomIconPrefab, Vector3.zero, Quaternion.identity);
            roomIcon.name = newRoomInfo.Name;
            roomIcon.gameObject.SetActive(true);
            roomIcon.transform.SetParent(roomListScrollRect.transform, false);
            roomIcon.UpdateUI(newRoomInfo);

            rooms[newRoomInfo.Name] = roomIcon;
        }

        private void RemoveRoom(RoomIcon icon) {
            if (EventSystem.current.currentSelectedGameObject == icon.gameObject) {
                // Move cursor so it doesn't get stuck.
                // TODO
            }

            Destroy(icon.gameObject);
            rooms.Remove(icon.room.Name);
        }

        public void ClearRooms() {
            foreach (RoomIcon room in rooms.Values) {
                Destroy(room.gameObject);
            }

            rooms.Clear();
        }

        //---Callbacks
        public void OnJoinedLobby() { }

        public void OnLeftLobby() {
            ClearRooms();
        }

        public void OnRoomListUpdate(List<RoomInfo> roomList) {
            foreach (RoomInfo newRoomInfo in roomList) {
                string roomName = newRoomInfo.Name;
                if (rooms.TryGetValue(roomName, out RoomIcon roomIcon)) {
                    // RoomIcon exists
                    if (newRoomInfo.RemovedFromList) {
                        // But we shouldn't display it anymore.
                        RemoveRoom(roomIcon);
                    } else {
                        // And it should still exist
                        roomIcon.UpdateUI(newRoomInfo);
                    }
                } else {
                    // RoomIcon doesn't exist
                    if (!newRoomInfo.RemovedFromList) {
                        // And it should
                        CreateRoom(newRoomInfo);
                    }
                }
            }
        }

        public void OnLobbyStatisticsUpdate(List<TypedLobbyInfo> lobbyStatistics) { }

        public void OnConnected() { }

        public void OnConnectedToMaster() { }

        public void OnDisconnected(DisconnectCause cause) {
            ClearRooms();
        }

        public void OnRegionListReceived(RegionHandler regionHandler) { }

        public void OnCustomAuthenticationResponse(Dictionary<string, object> data) { }

        public void OnCustomAuthenticationFailed(string debugMessage) { }
    }
}
