using Quantum;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NSMB.UI.MainMenu {
    public class PlayerListHandler : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private GameObject contentPane;
        [SerializeField] private List<PlayerListEntry> playerListEntries = new();

        //---Private Variables
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
            QuantumEvent.Subscribe<EventGameStateChanged>(this, OnGameStateChanged);
        }

        public unsafe void PopulatePlayerEntries(Frame f) {
            RemoveAllPlayerEntries();

            var playerDataFilter = f.Filter<PlayerData>();
            playerDataFilter.UseCulling = false;

            while (playerDataFilter.NextUnsafe(out _, out PlayerData* playerData)) {
                AddPlayerEntry(f, playerData->PlayerRef);
            }
        }

        public void AddPlayerEntry(Frame f, PlayerRef player) {
            if (!player.IsValid) {
                return;
            }

            if (!GetPlayerEntry(player)) {
                RuntimePlayer runtimePlayerData = f.GetPlayerData(player);
                PlayerListEntry newEntry = GetUnusedPlayerEntry();
                newEntry.name = $"{runtimePlayerData.PlayerNickname} ({runtimePlayerData.UserId})";
                newEntry.player = player;
            }

            UpdateAllPlayerEntries(f);
        }

        public void RemoveAllPlayerEntries() {
            foreach (PlayerListEntry entry in playerListEntries) {
                Destroy(entry.gameObject);
            }
            playerListEntries.Clear();
        }


        public void RemovePlayerEntry(Frame f, PlayerRef player) {
            PlayerListEntry existingEntry = GetPlayerEntry(player);
            if (existingEntry) {
                existingEntry.player = PlayerRef.None;
                UpdateAllPlayerEntries(f);
            }
        }

        public void UpdateAllPlayerEntries(Frame f) {
            foreach (var entry in playerListEntries) {
                entry.UpdateText(f);
            }

            ReorderEntries(f);
            if (MainMenuManager.Instance) {
                MainMenuManager.Instance.chat.UpdatePlayerColors();
            }
        }

        public PlayerListEntry GetPlayerEntry(PlayerRef player) {
            return playerListEntries.FirstOrDefault(ple => ple.player == player);
        }

        public PlayerListEntry GetUnusedPlayerEntry() {
            return playerListEntries.FirstOrDefault(ple => ple.player == PlayerRef.None);
        }

        public unsafe void ReorderEntries(Frame f) {
            var sortedEntries = playerListEntries.OrderByDescending(ple => {
                PlayerData* data = QuantumUtils.GetPlayerData(f, ple.player);
                if (data == null) {
                    return int.MaxValue;
                }
                return data->JoinTick;
            });

            foreach (PlayerListEntry entry in sortedEntries) {
                entry.transform.SetAsFirstSibling();
            }
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

        private void OnGameStateChanged(EventGameStateChanged e) {
            if (e.NewState == GameState.PreGameRoom) {
                UpdateAllPlayerEntries(e.Frame);
            }
        }

        private void OnGameDestroyed(CallbackGameDestroyed e) {
            RemoveAllPlayerEntries();
        }
    }
}
