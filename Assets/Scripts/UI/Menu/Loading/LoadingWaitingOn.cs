using System;
using System.Linq;
using UnityEngine;
using TMPro;

using NSMB.Extensions;
using NSMB.Game;
using NSMB.Translation;

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
        }

        public void OnEnable() {
            playerListParent.SetActive(false);
        }

        public void Update() {
            TranslationManager tm = GlobalController.Instance.translationManager;

            if (!NetworkHandler.Runner.TryGetLocalPlayerData(out PlayerData pd) || pd.IsCurrentlySpectating) {
                // Loading (as spectator)
                RunIfNewState(LoadingState.Spectator, () => {
                    statusText.text = tm.GetTranslation("ui.loading.spectator");
                    playerListParent.SetActive(false);
                });
            } else if (!pd.IsLoaded) {
                // *WE* are still loading
                RunIfNewState(LoadingState.Loading, () => {
                    statusText.text = tm.GetTranslation("ui.loading.loading");
                    playerListParent.SetActive(false);
                });
            } else if (GameManager.Instance.GameState >= Enums.GameState.Starting) {
                // Game starting
                RunIfNewState(LoadingState.Starting, () => {
                    statusText.text = tm.GetTranslation("ui.loading.starting");
                    playerListParent.SetActive(false);
                });

            } else {
                // Waiting for others
                // TODO: convert to use the state system, needs to update when the ready list changes
                statusText.text = tm.GetTranslation("ui.loading.waiting");
                playerListParent.SetActive(true);
                playerList.text =
                    string.Join('\n',
                        SessionData.Instance.PlayerDatas
                            .Select(kvp => kvp.Value)
                            .Where(data => !data.IsCurrentlySpectating && !data.IsLoaded)
                            .Select(data => data.GetNickname())
                    );
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
