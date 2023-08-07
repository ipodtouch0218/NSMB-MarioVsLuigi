using System.Collections.Generic;
using UnityEngine;
using TMPro;

using Fusion;
using NSMB.Extensions;
using NSMB.Game;
using NSMB.Translation;

namespace NSMB.Loading {

    [RequireComponent(typeof(TMP_Text))]
    public class LoadingWaitingOn : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private TMP_Text playerList;

        //---Private Variables
        private TMP_Text text;
        private GameObject playerListParent;
        private string ourNickname;

        public void Awake() {
            text = GetComponent<TMP_Text>();
            playerListParent = playerList.transform.parent.gameObject;
            text.text = GlobalController.Instance.translationManager.GetTranslation("ui.loading.loading");
        }

        public void OnEnable() {
            playerListParent.SetActive(false);
            ourNickname = NetworkHandler.Runner.GetLocalPlayerData()?.GetNickname();
        }

        public void Update() {

            TranslationManager tm = GlobalController.Instance.translationManager;
            PlayerData ourData = NetworkHandler.Runner.LocalPlayer.GetPlayerData(NetworkHandler.Runner);

            // Loading (as spectator)
            if (ourData.IsCurrentlySpectating) {
                text.text = tm.GetTranslation("ui.loading.spectator");
                playerListParent.SetActive(false);
                return;
            }

            // Still loading
            if (!GameData.Instance || !GameData.Instance.Object.IsValid) {
                text.text = tm.GetTranslation("ui.loading.loading");
                playerListParent.SetActive(false);
                return;
            }

            // Game starting
            if (GameData.Instance.GameStartTimer.IsRunning) {
                text.text = tm.GetTranslation("ui.loading.starting");
                playerListParent.SetActive(false);
                return;
            }

            HashSet<string> waitingFor = new();
            NetworkRunner runner = GameData.Instance.Runner;
            foreach (PlayerRef player in runner.ActivePlayers) {
                PlayerData data = player.GetPlayerData(runner);
                if (!data || data.IsCurrentlySpectating)
                    continue;

                if (!data.IsLoaded)
                    waitingFor.Add(data.GetNickname());
            }

            if (waitingFor.Contains(ourNickname)) {
                // Loading
                text.text = tm.GetTranslation("ui.loading.loading");
                playerListParent.SetActive(false);

            } else {
                // Waiting for others
                text.text = tm.GetTranslation("ui.loading.waiting");

                playerListParent.SetActive(true);
                playerList.text = waitingFor.Count == 0 ? "" : "\n- " + string.Join("\n- ", waitingFor);
            }
        }
    }
}
