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

            // Loading (as spectator)
            if (!NetworkHandler.Runner.TryGetLocalPlayerData(out PlayerData pd) || pd.IsCurrentlySpectating) {
                statusText.text = tm.GetTranslation("ui.loading.spectator");
                playerListParent.SetActive(false);

            } else if (!pd.IsLoaded) {
                // *WE* are still loading
                statusText.text = tm.GetTranslation("ui.loading.loading");
                playerListParent.SetActive(false);

            } else if (GameManager.Instance.GameState >= Enums.GameState.Starting) {
                // Game starting
                statusText.text = tm.GetTranslation("ui.loading.starting");
                playerListParent.SetActive(false);

            } else {
                // Waiting for others
                statusText.text = tm.GetTranslation("ui.loading.waiting");
                playerListParent.SetActive(true);
                playerList.text =
                    string.Join('\n',
                        SessionData.Instance.PlayerDatas
                            .Select(kvp => kvp.Value)
                            .Where(pd => !pd.IsCurrentlySpectating && !pd.IsLoaded)
                            .Select(pd => pd.GetNickname())
                    );
            }

        }
    }
}
