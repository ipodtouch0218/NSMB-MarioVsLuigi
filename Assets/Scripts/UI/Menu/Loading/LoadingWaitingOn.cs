using System.Text;
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
        [SerializeField] private TMP_Text statusText;

        //---Private Variables
        private GameObject playerListParent;

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
            PlayerData ourData = NetworkHandler.Runner.GetLocalPlayerData();

            // Loading (as spectator)
            if (!ourData || ourData.IsCurrentlySpectating) {
                statusText.text = tm.GetTranslation("ui.loading.spectator");
                playerListParent.SetActive(false);
                return;
            }

            // Still loading
            if (!GameManager.Instance || !GameManager.Instance.Object) {
                statusText.text = tm.GetTranslation("ui.loading.loading");
                playerListParent.SetActive(false);
                return;
            }

            // Game starting
            if (GameManager.Instance.gameObject.activeSelf && GameManager.Instance.GameStartTimer.IsRunning) {
                statusText.text = tm.GetTranslation("ui.loading.starting");
                playerListParent.SetActive(false);
                return;
            }

            // Waiting for others
            StringBuilder waitingOnPlayers = new();
            int waitingCount = 0;
            NetworkRunner runner = GameManager.Instance.Runner;
            foreach (PlayerRef player in runner.ActivePlayers) {
                PlayerData data = player.GetPlayerData();
                if (!data || data.IsCurrentlySpectating || data.Object.HasControlAuthority()) {
                    continue;
                }

                if (!data.IsLoaded) {
                    waitingOnPlayers.AppendLine(data.GetNickname());
                    waitingCount++;
                }
            }

            if (waitingCount <= 0) {
                playerListParent.SetActive(false);
            } else {
                statusText.text = tm.GetTranslation("ui.loading.waiting");
                playerListParent.SetActive(true);
                playerList.text = waitingOnPlayers.ToString();
            }
        }
    }
}
