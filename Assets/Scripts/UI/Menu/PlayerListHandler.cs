using NSMB.Extensions;
using Photon.Client;
using Photon.Realtime;
using System.Collections.Generic;
using UnityEngine;

using Quantum;
using System.Collections;
using System.Linq;

namespace NSMB.UI.MainMenu {
    public class PlayerListHandler : MonoBehaviour, IInRoomCallbacks {

        //---Serialized Variables
        [SerializeField] private GameObject contentPane, template;

        //---Private Variables
        private readonly Dictionary<PlayerRef, PlayerListEntry> playerListEntries = new();
        private Coroutine autoRefreshCoroutine;

        public void OnEnable() {
            NetworkHandler.Client.AddCallbackTarget(this);
            autoRefreshCoroutine = StartCoroutine(AutoUpdateCoroutine());
        }

        public void OnDisable() {
            RemoveAllPlayerEntries();
            if (autoRefreshCoroutine != null) {
                StopCoroutine(autoRefreshCoroutine);
                autoRefreshCoroutine = null;
            }
            NetworkHandler.Client?.RemoveCallbackTarget(this);
        }

        public void PopulatePlayerEntries() {
            RemoveAllPlayerEntries();

            foreach ((_, Player player) in NetworkHandler.Client.CurrentRoom.Players) {
                AddPlayerEntry(player);
            }
        }

        public void AddPlayerEntry(Player player) {
            if (player == null || !template) {
                return;
            }

            if (!playerListEntries.ContainsKey(player.ActorNumber)) {
                GameObject go = Instantiate(template, contentPane.transform);
                go.name = $"{player.NickName} ({player.UserId})";
                playerListEntries[player.ActorNumber] = go.GetComponent<PlayerListEntry>();
                playerListEntries[player.ActorNumber].player = player;
                go.SetActive(true);
            }

            UpdateAllPlayerEntries();
        }

        public void RemoveAllPlayerEntries() {
            foreach ((_, PlayerListEntry entry) in playerListEntries) {
                Destroy(entry.gameObject);
            }
            playerListEntries.Clear();
        }


        public void RemovePlayerEntry(Player player) {
            if (!playerListEntries.ContainsKey(player.ActorNumber)) {
                return;
            }

            Destroy(playerListEntries[player.ActorNumber].gameObject);
            playerListEntries.Remove(player.ActorNumber);
            UpdateAllPlayerEntries();
        }

        public void UpdateAllPlayerEntries() {
            foreach ((_, Player player) in NetworkHandler.Client.CurrentRoom.Players) {
                UpdatePlayerEntry(player, false);
            }

            if (MainMenuManager.Instance) {
                MainMenuManager.Instance.chat.UpdatePlayerColors();
            }
        }

        public void UpdatePlayerEntry(Player player, bool updateChat = true) {
            if (!playerListEntries.TryGetValue(player.ActorNumber, out PlayerListEntry entry)) {
                //AddPlayerEntry(data);
                return;
            }

            entry.UpdateText();
            ReorderEntries();

            if (updateChat && MainMenuManager.Instance) {
                MainMenuManager.Instance.chat.UpdatePlayerColors();
            }
        }

        public void ReorderEntries() {
            var playerList = NetworkHandler.Client.CurrentRoom.Players.OrderByDescending(kvp => kvp.Key);

            foreach ((PlayerRef player, _) in playerList) {
                if (!playerListEntries.TryGetValue(player, out PlayerListEntry entry)) {
                    continue;
                }

                entry.transform.SetAsFirstSibling();
            }
        }

        public PlayerListEntry GetPlayerListEntry(int actorNumber) {
            return playerListEntries.GetValueOrDefault(actorNumber);
        }

        private IEnumerator AutoUpdateCoroutine() {
            WaitForSeconds seconds = new(1);
            while (true) {
                yield return seconds;
                UpdateAllPlayerEntries();
            }
        }

        //---Callbacks
        public void OnPlayerEnteredRoom(Player newPlayer) {
            AddPlayerEntry(newPlayer);
            MainMenuManager.Instance.sfx.PlayOneShot(SoundEffect.UI_PlayerConnect);
        }

        public void OnPlayerLeftRoom(Player otherPlayer) {
            RemovePlayerEntry(otherPlayer);
        }

        public void OnRoomPropertiesUpdate(PhotonHashtable propertiesThatChanged) { }

        public void OnPlayerPropertiesUpdate(Player targetPlayer, PhotonHashtable changedProps) { }

        public void OnMasterClientSwitched(Player newMasterClient) { }
    }
}
