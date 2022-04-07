using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class SpectationManager : MonoBehaviour {

    [SerializeField] private GameObject spectationUI;
    [SerializeField] private TMP_Text spectatingText;
    private bool _spectating = false;
    public bool Spectating {
        get {
            return _spectating;
        }
        set {
            _spectating = value;
            UpdateSpectateUI();
        }
    }
    private PlayerController targetPlayer;
    private int targetIndex;

    public void Update() {
        if (!Spectating)
            return;

        if (targetPlayer == null)
            SpectateNextPlayer();

        UpdateSpectateUI();
    }

    public void UpdateSpectateUI() {
        spectationUI.SetActive(Spectating);
        if (!Spectating)
            return;

        spectatingText.text = $"Spectating: { targetPlayer.photonView.Owner.NickName }";
    }
    
    public void SpectateNextPlayer() {
        if (targetPlayer)
            targetPlayer.cameraController.controlCamera = false;

        targetPlayer = null;
        PlayerController[] players = GameManager.Instance.allPlayers;

        while (targetPlayer == null) {
            targetIndex = (targetIndex + 1) % players.Length;
            targetPlayer = players[targetIndex];
        }

        targetPlayer.cameraController.controlCamera = true;
    }

    public void SpectatePreviousPlayer() {
        if (targetPlayer)
            targetPlayer.cameraController.controlCamera = false;

        targetPlayer = null;
        PlayerController[] players = GameManager.Instance.allPlayers;

        while (targetPlayer == null) {
            targetIndex = (targetIndex - 1 + players.Length) % players.Length;
            targetPlayer = players[targetIndex];
        }

        targetPlayer.cameraController.controlCamera = true;
    }
}