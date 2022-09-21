using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;

using NSMB.Extensions;

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

        PlayerData ourData = NetworkHandler.Instance.runner.LocalPlayer.GetPlayerData(NetworkHandler.Instance.runner);

        if (ourData.IsCurrentlySpectating) {
            text.text = spectatorText;
            return;
        }

        if (GameManager.Instance.loaded) {
            text.text = readyToStartText;
            text.text = emptyText;
            playerList.text = "";
            return;
        }

        text.text = iveLoadedText;

        HashSet<string> waitingFor = new();
        foreach (PlayerController pc in GameManager.Instance.players) {
            PlayerData data = pc.Object.InputAuthority.GetPlayerData(pc.Runner);

            if (!data.IsCurrentlySpectating && !data.IsLoaded)
                waitingFor.Add(data.GetNickname());
        }
        playerList.text = waitingFor.Count == 0 ? "" : "Waiting for:\n\n- " + string.Join("\n- ", waitingFor);
    }
}
