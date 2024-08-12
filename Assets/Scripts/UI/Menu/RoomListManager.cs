using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

using NSMB.Utils;
using Photon.Realtime;

namespace NSMB.UI.MainMenu {
    public class RoomListManager : MonoBehaviour, ILobbyCallbacks {

        //---Properties
        public string SelectedRoomCode {
            get => selectedRoomCode;
            set {
                if (SelectedRoomIcon) {
                    SelectedRoomIcon.Unselect();
                }

                selectedRoomCode = value;
                joinRoomButton.interactable = value != null;

                if (SelectedRoomIcon) {
                    SelectedRoomIcon.Select();
                }
            }
        }

        public RoomIcon SelectedRoomIcon {
            get => rooms.FirstOrDefault(kvp => kvp.Key == selectedRoomCode).Value;
            set => selectedRoomCode = rooms.FirstOrDefault(kvp => kvp.Value == value).Key;
        }

        //---Serialized Variables
        [SerializeField] private RoomIcon roomIconPrefab, privateRoomIcon;
        [SerializeField] private GameObject roomListScrollRect, privateRoomIdPrompt;
        [SerializeField] private Button joinRoomButton;

        //---Private Variables
        private readonly Dictionary<string, RoomIcon> rooms = new();
        private float lastSelectTime;
        private string selectedRoomCode;

        public void Initialize() {
            NetworkHandler.Client.AddCallbackTarget(this);
            joinRoomButton.interactable = false;
        }

        public void OnDestroy() {
            NetworkHandler.Client?.RemoveCallbackTarget(this);
        }

        public void SelectRoom(RoomIcon room) {
            if (SelectedRoomIcon == room) {
                if (Time.time - lastSelectTime < 0.3f) {
                    // Double-click
                    JoinSelectedRoom();
                    return;
                }
            }

            SelectedRoomIcon = room;
            joinRoomButton.interactable = SelectedRoomIcon && Settings.Instance.generalNickname.IsValidUsername(false);
            lastSelectTime = Time.time;
        }

        public async void JoinSelectedRoom() {
            if (SelectedRoomCode == null) {
                return;
            }

            await NetworkHandler.JoinRoom(new EnterRoomArgs() {
                RoomName = SelectedRoomCode
            });
            // TODO await NetworkHandler.JoinRoom(SelectedRoom.session.Name);
        }

        public void OpenPrivateRoomPrompt() {
            privateRoomIdPrompt.SetActive(true);
        }

        public void RefreshRooms() {
            foreach (RoomIcon room in rooms.Values) {
                // TODO room.UpdateUI(room.session);
            }
        }

        public void RemoveRoom(RoomIcon icon) {
            Destroy(icon.gameObject);
            // TODO rooms.Remove(icon.session.Name);

            if (SelectedRoomIcon == icon) {
                SelectedRoomIcon = null;
            }
        }

        public void ClearRooms() {
            foreach (RoomIcon room in rooms.Values) {
                Destroy(room.gameObject);
            }

            rooms.Clear();
            SelectedRoomCode = null;
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
                        Destroy(roomIcon.gameObject);
                        rooms.Remove(roomName);
                    } else {
                        // And it should still exist
                        roomIcon.UpdateUI(newRoomInfo);
                    }
                } else {
                    // RoomIcon doesn't exist
                    if (!newRoomInfo.RemovedFromList) {
                        // And it should
                        roomIcon = Instantiate(roomIconPrefab, Vector3.zero, Quaternion.identity);
                        roomIcon.name = newRoomInfo.Name;
                        roomIcon.gameObject.SetActive(true);
                        roomIcon.transform.SetParent(roomListScrollRect.transform, false);
                        roomIcon.UpdateUI(newRoomInfo);

                        rooms[newRoomInfo.Name] = roomIcon;
                    }
                }
            }


            /*
            List<string> invalidRooms = rooms.Keys.ToList();

            foreach (SessionInfo session in sessionList) {

                // Is this check really necessary anymore?

                NetworkUtils.GetSessionProperty(session, Enums.NetRoomProperties.HostName, out string host);
                NetworkUtils.GetSessionProperty(session, Enums.NetRoomProperties.IntProperties, out int packedIntProperties);
                NetworkUtils.IntegerProperties intProperties = (NetworkUtils.IntegerProperties) packedIntProperties;

                bool valid = true;
                valid &= session.IsVisible && session.IsOpen;
                valid &= session.MaxPlayers > 0 && session.MaxPlayers <= 10;
                valid &= intProperties.maxPlayers > 0 && intProperties.maxPlayers <= 10;
                valid &= intProperties.lives <= 25;
                valid &= intProperties.starRequirement >= 1 && intProperties.starRequirement <= 25;
                valid &= intProperties.coinRequirement >= 3 && intProperties.coinRequirement <= 25;
                valid &= host.IsValidUsername();

                if (valid) {
                    invalidRooms.Remove(session.Name);
                } else {
                    continue;
                }

                RoomIcon roomIcon;
                if (rooms.ContainsKey(session.Name)) {
                    roomIcon = rooms[session.Name];
                } else {
                    roomIcon = Instantiate(roomIconPrefab, Vector3.zero, Quaternion.identity);
                    roomIcon.name = session.Name;
                    roomIcon.gameObject.SetActive(true);
                    roomIcon.transform.SetParent(roomListScrollRect.transform, false);
                    roomIcon.session = session;

                    rooms[session.Name] = roomIcon;
                }

                roomIcon.UpdateUI(session);
            }

            foreach (string key in invalidRooms) {
                if (!rooms.ContainsKey(key)) {
                    continue;
                }

                Destroy(rooms[key].gameObject);
                rooms.Remove(key);
            }

            //if (askedToJoin && selectedRoom != null) {
            //    JoinSelectedRoom();
            //    askedToJoin = false;
            //    selectedRoom = null;
            //}

            //privateRoomIcon.transform.SetAsFirstSibling();
            */
        }

        public void OnLobbyStatisticsUpdate(List<TypedLobbyInfo> lobbyStatistics) { }
    }
}
