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
            //RemoveAllPlayerEntries();
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

        public unsafe void PopulatePlayerEntries(Frame f) {
            RemoveAllPlayerEntries();

            var playerDataFilter = f.Filter<PlayerData>();

            while (playerDataFilter.NextUnsafe(out _, out PlayerData* playerData)) {
                AddPlayerEntry(f, playerData->PlayerRef);
            }
        }

        public void AddPlayerEntry(Frame f, PlayerRef player) {
            if (!player.IsValid || !template) {
                return;
            }

            RuntimePlayer runtimePlayerData = f.GetPlayerData(player);

            if (!playerListEntries.ContainsKey(player)) {
                GameObject go = Instantiate(template, contentPane.transform);
                go.name = $"{runtimePlayerData.PlayerNickname} ({runtimePlayerData.UserId})";
                playerListEntries[player] = go.GetComponent<PlayerListEntry>();
                playerListEntries[player].player = player;
                go.SetActive(true);
            }

            UpdateAllPlayerEntries(f);
        }

        public void RemoveAllPlayerEntries() {
            foreach ((_, PlayerListEntry entry) in playerListEntries) {
                Destroy(entry.gameObject);
            }
            playerListEntries.Clear();
        }


        public void RemovePlayerEntry(Frame f, PlayerRef player) {
            if (!playerListEntries.ContainsKey(player)) {
                return;
            }

            Destroy(playerListEntries[player].gameObject);
            playerListEntries.Remove(player);
            UpdateAllPlayerEntries(f);
        }

        public void UpdateAllPlayerEntries(Frame f) {
            foreach ((PlayerRef player, _) in playerListEntries) {
                UpdatePlayerEntry(f, player, false);
            }

            if (MainMenuManager.Instance) {
                MainMenuManager.Instance.chat.UpdatePlayerColors();
            }
        }

        public void UpdatePlayerEntry(Frame f, PlayerRef player, bool updateChat = true) {
            if (!playerListEntries.TryGetValue(player, out PlayerListEntry entry)) {
                //AddPlayerEntry(data);
                return;
            }

            entry.UpdateText(f);
            ReorderEntries(f);

            if (updateChat && MainMenuManager.Instance) {
                MainMenuManager.Instance.chat.UpdatePlayerColors();
            }
        }

        public unsafe void ReorderEntries(Frame f) {
            var sortedEntries = playerListEntries.OrderByDescending(ple => {
                PlayerData* data = QuantumUtils.GetPlayerData(f, ple.Key);
                if (data == null) {
                    return int.MaxValue;
                }
                return data->JoinTick;
            });

            foreach ((PlayerRef player, PlayerListEntry entry) in sortedEntries) {
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
                if (QuantumRunner.DefaultGame != null) {
                    UpdateAllPlayerEntries(QuantumRunner.DefaultGame.Frames.Predicted);
                }
            }
        }

        //---Callbacks
        private void OnPlayerAdded(EventPlayerAdded e) {
            AddPlayerEntry(e.Frame, e.Player);
        }

        private void OnPlayerRemoved(EventPlayerRemoved e) {
            RemovePlayerEntry(e.Frame, e.Player);
        }

        private void OnGameDestroyed(CallbackGameDestroyed e) {
            RemoveAllPlayerEntries();
        }
    }
}
