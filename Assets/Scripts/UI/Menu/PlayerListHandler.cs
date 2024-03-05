using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using Fusion;

namespace NSMB.UI.MainMenu {
    public class PlayerListHandler : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private GameObject contentPane, template;

        //---Private Variables
        private readonly Dictionary<PlayerRef, PlayerListEntry> playerListEntries = new();

        //---Properties
        private NetworkRunner Runner => NetworkHandler.Instance.runner;

        public void OnEnable() {
            if (!NetworkHandler.Instance || !Runner) {
                return;
            }

            //NetworkHandler.OnPlayerJoined += OnPlayerJoined;
            NetworkHandler.OnPlayerLeft += OnPlayerLeft;
            PlayerData.OnPlayerDataReady += OnPlayerDataReady;
        }

        public void OnDisable() {
            RemoveAllPlayerEntries();

            //NetworkHandler.OnPlayerJoined -= OnPlayerJoined;
            NetworkHandler.OnPlayerLeft -= OnPlayerLeft;
            PlayerData.OnPlayerDataReady -= OnPlayerDataReady;
        }

        public void PopulatePlayerEntries() {
            RemoveAllPlayerEntries();

            if (!SessionData.Instance) {
                return;
            }

            foreach ((_, PlayerData data) in SessionData.Instance.PlayerDatas) {
                AddPlayerEntry(data);
            }
        }

        public void AddPlayerEntry(PlayerData data) {
            if (!data || !template) {
                return;
            }
            PlayerRef player = data.Owner;

            if (!playerListEntries.ContainsKey(player)) {
                GameObject go = Instantiate(template, contentPane.transform);
                go.name = $"{data.GetNickname()} ({data.GetUserIdString()})";
                playerListEntries[player] = go.GetComponent<PlayerListEntry>();
                playerListEntries[player].player = data;
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

        public void RemovePlayerEntry(PlayerRef player) {
            if (!playerListEntries.ContainsKey(player)) {
                return;
            }

            Destroy(playerListEntries[player].gameObject);
            playerListEntries.Remove(player);
            UpdateAllPlayerEntries();
        }

        public void UpdateAllPlayerEntries() {
            foreach ((_, PlayerData data) in SessionData.Instance.PlayerDatas) {
                UpdatePlayerEntry(data, false);
            }

            if (MainMenuManager.Instance) {
                MainMenuManager.Instance.chat.UpdatePlayerColors();
            }
        }

        public void UpdatePlayerEntry(PlayerData data, bool updateChat = true) {
            if (!playerListEntries.ContainsKey(data.Owner)) {
                //AddPlayerEntry(data);
                return;
            }

            playerListEntries[data.Owner].UpdateText();
            ReorderEntries();

            if (updateChat && MainMenuManager.Instance) {
                MainMenuManager.Instance.chat.UpdatePlayerColors();
            }
        }

        public void ReorderEntries() {
            var playerList = SessionData.Instance.PlayerDatas.OrderByDescending(pd => pd.Value.JoinTick);

            foreach ((PlayerRef player, _) in playerList) {
                if (!playerListEntries.ContainsKey(player)) {
                    continue;
                }

                playerListEntries[player].transform.SetAsFirstSibling();
            }
        }

        public PlayerListEntry GetPlayerListEntry(PlayerRef player) {
            if (playerListEntries.ContainsKey(player)) {
                return playerListEntries[player];
            }

            return null;
        }

        //---Callbacks
        public void OnPlayerDataReady(PlayerData data) {
            AddPlayerEntry(data);
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) {
            RemovePlayerEntry(player);
        }
    }
}
