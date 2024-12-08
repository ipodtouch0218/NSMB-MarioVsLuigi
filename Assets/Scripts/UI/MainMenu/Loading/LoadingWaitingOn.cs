using NSMB.Extensions;
using NSMB.Translation;
using Quantum;
using System.Collections.Generic;
using System;
using TMPro;
using UnityEngine;
using System.Text;
using NSMB.Utils;

namespace NSMB.Loading {

    [RequireComponent(typeof(TMP_Text))]
    public class LoadingWaitingOn : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private TMP_Text playerList;
        [SerializeField] private TMP_Text statusText;

        //---Private Variables
        private GameObject playerListParent;
        private LoadingState currentLoadingState = LoadingState.None;

        public void OnValidate() {
            this.SetIfNull(ref statusText);
        }

        public void Awake() {
            playerListParent = playerList.transform.parent.gameObject;
            statusText.text = GlobalController.Instance.translationManager.GetTranslation("ui.loading.loading");

            QuantumCallback.Subscribe<CallbackUpdateView>(this, OnUpdateView, onlyIfActiveAndEnabled: true);
        }

        public void OnEnable() {
            playerListParent.SetActive(false);
        }

        public unsafe void OnUpdateView(CallbackUpdateView e) {
            QuantumGame game = e.Game;
            Frame f = e.Game.Frames.Predicted;
            TranslationManager tm = GlobalController.Instance.translationManager;

            PlayerRef localPlayer = default;
            if (game.GetLocalPlayers().Count > 0) {
                localPlayer = game.GetLocalPlayers()[0];
            }
            PlayerData* playerData = QuantumUtils.GetPlayerData(f, localPlayer);

            if (playerData == null || f.Global->GameState >= GameState.Playing) {
                // Loading (as spectator)
                RunIfNewState(LoadingState.Spectator, () => {
                    statusText.text = tm.GetTranslation("ui.loading.spectator");
                    playerListParent.SetActive(false);
                });
            } else if (!playerData->IsLoaded) {
                // *WE* are still loading
                RunIfNewState(LoadingState.Loading, () => {
                    statusText.text = tm.GetTranslation("ui.loading.loading");
                    playerListParent.SetActive(false);
                });
            } else if (f.Global->GameState >= GameState.Starting) {
                // Game starting
                RunIfNewState(LoadingState.Starting, () => {
                    statusText.text = tm.GetTranslation("ui.loading.starting");
                    playerListParent.SetActive(false);
                });
            } else {
                // Waiting for others
                // TODO: convert to use the state system, needs to update when the ready list changes
                int secondsUntilKick = (int) Mathf.Max(0, (f.Global->PlayerLoadFrames * f.DeltaTime).AsFloat);
                statusText.text = secondsUntilKick <= 10 ? secondsUntilKick.ToString() : tm.GetTranslation("ui.loading.waiting");

                StringBuilder loadingListBuilder = new();
                var playerDataFilter = f.Filter<PlayerData>();
                while (playerDataFilter.NextUnsafe(out _, out PlayerData* otherPlayerData)) {
                    if (otherPlayerData->IsLoaded) {
                        continue;
                    }

                    RuntimePlayer runtimePlayer = f.GetPlayerData(otherPlayerData->PlayerRef);
                    if (runtimePlayer == null) {
                        continue;
                    }

                    loadingListBuilder.AppendLine(runtimePlayer.PlayerNickname.ToValidUsername(f, otherPlayerData->PlayerRef));
                }
                playerListParent.SetActive(true);
                playerList.text = loadingListBuilder.ToString();
            }
        }

        private void RunIfNewState(LoadingState newState, Action action) {
            if (currentLoadingState == newState) {
                return;
            }

            currentLoadingState = newState;
            action();
        }

        private enum LoadingState {
            None,
            Loading,
            Waiting,
            Starting,
            Spectator,
        }
    }
}
