using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using Fusion;
using NSMB.Extensions;

namespace NSMB.UI.MainMenu {
    public class PlayerListHandler : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private GameObject contentPane, template;

        //---Private Variables
        private readonly Dictionary<PlayerRef, PlayerListEntry> playerListEntries = new();

        //---Properties
        private NetworkRunner Runner => NetworkHandler.Instance.runner;

        public void OnEnable() {
            if (!NetworkHandler.Instance.runner)
                return;

            if (NetworkHandler.Instance.runner.SessionInfo.IsValid)
                PopulatePlayerEntries(true);

            NetworkHandler.OnPlayerJoined += OnPlayerJoined;
            NetworkHandler.OnPlayerLeft += OnPlayerLeft;
        }

        public void OnDisable() {
            RemoveAllPlayerEntries();

            NetworkHandler.OnPlayerJoined -= OnPlayerJoined;
            NetworkHandler.OnPlayerLeft -= OnPlayerLeft;
        }

        public void PopulatePlayerEntries(bool addSelf) {
            RemoveAllPlayerEntries();
            try {
                foreach (PlayerRef player in Runner.ActivePlayers) {
                    if (addSelf || Runner.LocalPlayer != player)
                        AddPlayerEntry(player);
                }
            } catch {

            }
        }

        public void AddPlayerEntry(PlayerRef player) {
            PlayerData data = player.GetPlayerData(Runner);
            if (!data || !template)
                return;

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
            if (!playerListEntries.ContainsKey(player))
                return;

            Destroy(playerListEntries[player].gameObject);
            playerListEntries.Remove(player);
            UpdateAllPlayerEntries();
        }

        public void UpdateAllPlayerEntries() {
            foreach (PlayerRef player in Runner.ActivePlayers)
                UpdatePlayerEntry(player, false);

            if (MainMenuManager.Instance)
                MainMenuManager.Instance.chat.UpdatePlayerColors();
        }

        public void UpdatePlayerEntry(PlayerRef player, bool updateChat = true) {
            if (!playerListEntries.ContainsKey(player)) {
                AddPlayerEntry(player);
                return;
            }

            playerListEntries[player].UpdateText();
            ReorderEntries();

            if (updateChat && MainMenuManager.Instance)
                MainMenuManager.Instance.chat.UpdatePlayerColors();
        }

        public void ReorderEntries() {
            foreach (PlayerRef player in Runner.ActivePlayers.OrderByDescending(pr => pr.GetPlayerData(NetworkHandler.Runner).JoinTick)) {

                if (!playerListEntries.ContainsKey(player))
                    continue;

                playerListEntries[player].transform.SetAsFirstSibling();
            }
        }

        public PlayerListEntry GetPlayerListEntry(PlayerRef player) {
            if (playerListEntries.ContainsKey(player))
                return playerListEntries[player];

            return null;
        }

        //---Callbacks
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) {
            AddPlayerEntry(player);
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) {
            RemovePlayerEntry(player);
        }
    }
}
