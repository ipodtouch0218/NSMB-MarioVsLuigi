using Quantum;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NSMB.UI.MainMenu {
    public class PlayerListHandler : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private GameObject contentPane, template;

        //---Private Variables
        private readonly Dictionary<PlayerRef, PlayerListEntry> playerListEntries = new();
        private Coroutine autoRefreshCoroutine;

        public void OnEnable() {
            autoRefreshCoroutine = StartCoroutine(AutoUpdateCoroutine());
        }

        public void OnDisable() {
            RemoveAllPlayerEntries();
            if (autoRefreshCoroutine != null) {
                StopCoroutine(autoRefreshCoroutine);
                autoRefreshCoroutine = null;
            }
        }

        public void Start() {
            QuantumCallback.Subscribe<CallbackGameDestroyed>(this, OnGameDestroyed);
            QuantumEvent.Subscribe<EventPlayerAdded>(this, OnPlayerAdded);
            QuantumEvent.Subscribe<EventPlayerRemoved>(this, OnPlayerRemoved);
        }

        public unsafe void PopulatePlayerEntries(QuantumGame game) {
            RemoveAllPlayerEntries();

            Frame f = game.Frames.Predicted;
            var playerDataFilter = f.Filter<PlayerData>();

            while (playerDataFilter.NextUnsafe(out _, out PlayerData* playerData)) {
                AddPlayerEntry(game, playerData->PlayerRef);
            }
        }

        public void AddPlayerEntry(QuantumGame game, PlayerRef player) {
            if (player == null || !template) {
                return;
            }

            Frame f = game.Frames.Predicted;
            RuntimePlayer runtimePlayerData = f.GetPlayerData(player);

            if (!playerListEntries.ContainsKey(player)) {
                GameObject go = Instantiate(template, contentPane.transform);
                go.name = $"{runtimePlayerData.PlayerNickname} ({runtimePlayerData.UserId})";
                playerListEntries[player] = go.GetComponent<PlayerListEntry>();
                playerListEntries[player].player = player;
                go.SetActive(true);
            }

            UpdateAllPlayerEntries(game);
        }

        public void RemoveAllPlayerEntries() {
            foreach ((_, PlayerListEntry entry) in playerListEntries) {
                Destroy(entry.gameObject);
            }
            playerListEntries.Clear();
        }


        public void RemovePlayerEntry(QuantumGame game, PlayerRef player) {
            if (!playerListEntries.ContainsKey(player)) {
                return;
            }

            Destroy(playerListEntries[player].gameObject);
            playerListEntries.Remove(player);
            UpdateAllPlayerEntries(game);
        }

        public void UpdateAllPlayerEntries(QuantumGame game) {
            foreach ((PlayerRef player, _) in playerListEntries) {
                UpdatePlayerEntry(game, player, false);
            }

            if (MainMenuManager.Instance) {
                MainMenuManager.Instance.chat.UpdatePlayerColors();
            }
        }

        public void UpdatePlayerEntry(QuantumGame game, PlayerRef player, bool updateChat = true) {
            if (!playerListEntries.TryGetValue(player, out PlayerListEntry entry)) {
                //AddPlayerEntry(data);
                return;
            }

            entry.UpdateText(game);
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

        public PlayerListEntry GetPlayerListEntry(PlayerRef player) {
            return playerListEntries.GetValueOrDefault(player);
        }

        private IEnumerator AutoUpdateCoroutine() {
            WaitForSeconds seconds = new(1);
            while (true) {
                yield return seconds;
                UpdateAllPlayerEntries(QuantumRunner.DefaultGame);
            }
        }

        //---Callbacks
        private void OnPlayerAdded(EventPlayerAdded e) {
            AddPlayerEntry(e.Game, e.Player);
        }

        private void OnPlayerRemoved(EventPlayerRemoved e) {
            RemovePlayerEntry(e.Game, e.Player);
        }

        private void OnGameDestroyed(CallbackGameDestroyed e) {
            RemoveAllPlayerEntries();
        }
    }
}
