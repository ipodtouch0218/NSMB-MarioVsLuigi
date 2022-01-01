using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Photon.Pun;
public class LoadingWaitingOn : MonoBehaviour {
    private TMP_Text text;
    public string emptyText = "";
    public string iveLoadedText = "";
    void Start() {
        text = GetComponent<TMP_Text>();
    }

    void Update() {
        if (GameManager.Instance.loadedPlayers.Count == 0) return;

        string final;
        if (iveLoadedText != null) {
            final = iveLoadedText;
        } else {
            List<string> loading = new List<string>();
            foreach (Photon.Realtime.Player player in PhotonNetwork.CurrentRoom.Players.Values) {
                if (!GameManager.Instance.loadedPlayers.Contains(player.NickName)) {
                    loading.Add(player.NickName);
                }
            }
            
            if (loading.Count <= 0) {
                text.text = emptyText; 
                return; 
            }
            final = "Waiting on:";
            foreach (string loadingPlayer in loading) {
                final += "\n* " + loadingPlayer;
            }
        }
        text.text = final;
    }
}
