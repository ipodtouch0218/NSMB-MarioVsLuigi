using NSMB.Networking;
using NSMB.Utilities.Extensions;
using Photon.Client;
using Photon.Realtime;
using Quantum;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;

namespace NSMB.UI.MainMenu.Submenus.InRoom {
    public class PlayerListHandler : MonoBehaviour, IInRoomCallbacks {

        //---Events
        public static event Action<int> PlayerAdded;
        public static event Action<int> PlayerRemoved;

        //---Properties
        public PlayerListEntry OpenDropdown => playerListEntries.FirstOrDefault(ple => ple.IsDropdownOpen);

        //---Serialized Variables
        [SerializeField] private MainMenuCanvas canvas;
        [SerializeField] private GameObject contentPane;
        [SerializeField] private PlayerListEntry template;

        //---Private Variables
        private Coroutine autoRefreshCoroutine;
        private readonly List<PlayerListEntry> playerListEntries = new(Constants.MaxPlayers);

        public void Initialize() {
            playerListEntries.Add(template);
            for (int i = 1; i < Constants.MaxPlayers; i++) {
                playerListEntries.Add(Instantiate(template, template.transform.parent));
            }

            QuantumCallback.Subscribe<CallbackGameStarted>(this, OnGameStarted);
            QuantumCallback.Subscribe<CallbackGameDestroyed>(this, OnGameDestroyed);
            QuantumEvent.Subscribe<EventPlayerAdded>(this, OnPlayerAdded, FilterOutReplay);
            QuantumEvent.Subscribe<EventPlayerRemoved>(this, OnPlayerRemoved, FilterOutReplay);
            QuantumEvent.Subscribe<EventGameStateChanged>(this, OnGameStateChanged);
            QuantumEvent.Subscribe<EventRulesChanged>(this, OnRulesChanged);
        }

        public void OnEnable() {
            // autoRefreshCoroutine = StartCoroutine(AutoUpdateCoroutine());
            ReorderEntries();

            if (NetworkHandler.Client != null) {
                UpdateLocks();
                NetworkHandler.Client.AddCallbackTarget(this);
            }
        }

        public void OnDisable() {
            //RemoveAllPlayerEntries();
            if (autoRefreshCoroutine != null) {
                StopCoroutine(autoRefreshCoroutine);
                autoRefreshCoroutine = null;
            }
            if (NetworkHandler.Client != null) {
                NetworkHandler.Client.RemoveCallbackTarget(this);
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
                PlayerListEntry newEntry = GetPlayerEntry(PlayerRef.None);
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
            foreach (var ple in playerListEntries) {
                if (ple.player == player) {
                    return ple;
                }
            }
            return null;
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

        private void UpdateLocks() {
            int maxPlayers = (NetworkHandler.Client.CurrentRoom == null) ? Constants.MaxPlayers : NetworkHandler.Client.CurrentRoom.MaxPlayers;

            for (int i = 0; i < playerListEntries.Count; i++) {
                playerListEntries[i].lockImage.gameObject.SetActive(i >= maxPlayers);
            }
        }

        //---Callbacks
        private void OnPlayerAdded(EventPlayerAdded e) {
            Frame f = e.Game.Frames.Verified;
            AddPlayerEntry(f, e.Player);
            GlobalController.Instance.sfx.PlayOneShot(SoundEffect.UI_PlayerConnect);
        }

        private void OnPlayerRemoved(EventPlayerRemoved e) {
            Frame f = e.Game.Frames.Verified;
            RemovePlayerEntry(f, e.Player);
            GlobalController.Instance.sfx.PlayOneShot(SoundEffect.UI_PlayerDisconnect);
        }

        private void OnGameStateChanged(EventGameStateChanged e) {
            Frame f = e.Game.Frames.Verified;
            if (e.NewState == GameState.PreGameRoom) {
                UpdateAllPlayerEntries(f);
            }
        }

        private void OnRulesChanged(EventRulesChanged e) {
            Frame f = e.Game.Frames.Verified;
            UpdateAllPlayerEntries(f);
        }

        private void OnGameDestroyed(CallbackGameDestroyed e) {
            RemoveAllPlayerEntries();
        }

        private void OnGameStarted(CallbackGameStarted e) {
            PopulatePlayerEntries(e.Game.Frames.Predicted);
        }

        public void OnPlayerEnteredRoom(Player newPlayer) { }

        public void OnPlayerLeftRoom(Player otherPlayer) { }

        public void OnRoomPropertiesUpdate(PhotonHashtable propertiesThatChanged) {
            UpdateLocks();
        }

        public void OnPlayerPropertiesUpdate(Player targetPlayer, PhotonHashtable changedProps) { }

        public void OnMasterClientSwitched(Player newMasterClient) { }
    }
}
