using Quantum;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NSMB.UI.MainMenu {
    public class PlayerListHandler : MonoBehaviour {

        //---Events
        public static event Action<int> PlayerAdded;
        public static event Action<int> PlayerRemoved;

        //---Serialized Variables
        [SerializeField] private MainMenuCanvas canvas;
        [SerializeField] private GameObject contentPane;
        [SerializeField] private PlayerListEntry template;

        //---Private Variables
        private Coroutine autoRefreshCoroutine;
        private readonly List<PlayerListEntry> playerListEntries = new(10);

        public void Initialize() {
            playerListEntries.Add(template);
            for (int i = 1; i < 10; i++) {
                playerListEntries.Add(Instantiate(template, template.transform.parent));
            }

            QuantumCallback.Subscribe<CallbackLocalPlayerAddConfirmed>(this, OnLocalPlayerAddConfirmed);
            QuantumCallback.Subscribe<CallbackGameDestroyed>(this, OnGameDestroyed);
            QuantumEvent.Subscribe<EventPlayerAdded>(this, OnPlayerAdded);
            QuantumEvent.Subscribe<EventPlayerRemoved>(this, OnPlayerRemoved);
            QuantumEvent.Subscribe<EventGameStateChanged>(this, OnGameStateChanged);
        }

        public void OnEnable() {
            autoRefreshCoroutine = StartCoroutine(AutoUpdateCoroutine());
            ReorderEntries();
        }

        public void OnDisable() {
            //RemoveAllPlayerEntries();
            if (autoRefreshCoroutine != null) {
                StopCoroutine(autoRefreshCoroutine);
                autoRefreshCoroutine = null;
            }
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
                PlayerListEntry newEntry = GetUnusedPlayerEntry();
                newEntry.SetPlayer(f, player);
                UpdateAllPlayerEntries(f);

                PlayerAdded?.Invoke(playerListEntries.IndexOf(newEntry));
            }
        }

        public void RemoveAllPlayerEntries() {
            foreach (PlayerListEntry entry in playerListEntries) {
                entry.RemovePlayer();
            }
        }

        public void RemovePlayerEntry(Frame f, PlayerRef player) {
            PlayerListEntry existingEntry = GetPlayerEntry(player);
            if (existingEntry) {
                PlayerRemoved?.Invoke(playerListEntries.IndexOf(existingEntry));
                existingEntry.RemovePlayer();
                UpdateAllPlayerEntries(f);
            }
        }

        public void UpdateAllPlayerEntries(Frame f) {
            foreach (var entry in playerListEntries) {
                entry.UpdateText(f);
            }

            ReorderEntries();
        }

        public PlayerListEntry GetPlayerEntry(PlayerRef player) {
            return playerListEntries.FirstOrDefault(ple => ple.player == player);
        }

        public PlayerListEntry GetUnusedPlayerEntry() {
            return playerListEntries.FirstOrDefault(ple => ple.player == PlayerRef.None);
        }

        public PlayerListEntry GetPlayerEntryAtIndex(int index) {
            return playerListEntries[index];
        }

        public int GetPlayerEntryIndexOf(PlayerListEntry entry) {
            return playerListEntries.IndexOf(entry);
        }

        public unsafe void ReorderEntries() {
            playerListEntries.Sort((a, b) => {
                return a.joinTick - b.joinTick;
            });

            for (int i = 0; i < playerListEntries.Count; i++) {
                PlayerListEntry entry = playerListEntries[i];
                entry.transform.SetAsLastSibling();

                UnityEngine.UI.Navigation navigation = new() {
                    mode = UnityEngine.UI.Navigation.Mode.Explicit,
                };
                if (i > 0) {
                    navigation.selectOnUp = playerListEntries[i - 1].button;
                }
                if (i < playerListEntries.Count - 1 && playerListEntries[i + 1].player.IsValid) {
                    navigation.selectOnDown = playerListEntries[i + 1].button;
                }
                entry.button.navigation = navigation;
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
            canvas.PlaySound(SoundEffect.UI_PlayerConnect);
        }

        private void OnPlayerRemoved(EventPlayerRemoved e) {
            RemovePlayerEntry(e.Frame, e.Player);
            canvas.PlaySound(SoundEffect.UI_PlayerDisconnect);
        }

        private void OnGameStateChanged(EventGameStateChanged e) {
            if (e.NewState == GameState.PreGameRoom) {
                UpdateAllPlayerEntries(e.Frame);
            }
        }

        private void OnGameDestroyed(CallbackGameDestroyed e) {
            RemoveAllPlayerEntries();
        }

        private void OnLocalPlayerAddConfirmed(CallbackLocalPlayerAddConfirmed e) {
            PopulatePlayerEntries(e.Frame);
        }
    }
}
