using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;

using Photon.Realtime;
using NSMB.Utils;

public class LoadingWaitingOn : MonoBehaviour {

    [SerializeField] private TMP_Text playerList;
    [SerializeField] private string emptyText = "Loading...", iveLoadedText = "Waiting for others...", readyToStartText = "Starting...", spectatorText = "Joining as Spectator...";

    private TMP_Text text;

    public void Start() {
        text = GetComponent<TMP_Text>();
    }

    public void Update() {
        if (!GameManager.Instance)
            return;

        if (GlobalController.Instance.joinedAsSpectator) {
            text.text = spectatorText;
            return;
        }

        if (GameManager.Instance.loaded) {
            text.text = readyToStartText;
            playerList.text = "";
            return;
        }

        if (GameManager.Instance.loadedPlayers.Count == 0) {
            text.text = emptyText;
            return;
        }

        text.text = iveLoadedText;

        HashSet<Player> waitingFor = new(GameManager.Instance.nonSpectatingPlayers);
        waitingFor.ExceptWith(GameManager.Instance.loadedPlayers);
        playerList.text = (waitingFor.Count) == 0 ? "" : "Waiting for:\n\n- " + string.Join("\n- ", waitingFor.Select(pl => pl.GetUniqueNickname()));
    }
}
