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
            if (TargetPlayer == null)
                SpectateNextPlayer();

            UpdateSpectateUI();
        }
    }
    private PlayerController _targetPlayer;
    public PlayerController TargetPlayer {
        get {
            return _targetPlayer;
        }
        set {
            _targetPlayer = value;
            if (value != null)
                UpdateSpectateUI();
        }
    }
    private int targetIndex;

    public void Update() {
        if (!Spectating)
            return;

        if (!TargetPlayer)
            SpectateNextPlayer();
    }

    public void UpdateSpectateUI() {
        spectationUI.SetActive(Spectating);
        if (!Spectating)
            return;
        if (!TargetPlayer)
            return;
        
        spectatingText.text = $"Spectating: { TargetPlayer.photonView.Owner.NickName }";
    }
    
    public void SpectateNextPlayer() {
        if (TargetPlayer)
            TargetPlayer.cameraController.controlCamera = false;

        TargetPlayer = null;
        PlayerController[] players = GameManager.Instance.allPlayers;
        if (players.Length <= 0) {
            GameManager.Instance.allPlayers = FindObjectsOfType<PlayerController>();
            players = GameManager.Instance.allPlayers;
        }
        if (players.Length <= 0)
            return;

        int nulls = 0;
        while (TargetPlayer == null) {
            targetIndex = (targetIndex + 1) % players.Length;
            TargetPlayer = players[targetIndex];
            if (nulls++ >= players.Length)
                break;
        }

        TargetPlayer.cameraController.controlCamera = true;
    }

    public void SpectatePreviousPlayer() {
        if (TargetPlayer)
            TargetPlayer.cameraController.controlCamera = false;

        TargetPlayer = null;
        PlayerController[] players = GameManager.Instance.allPlayers;
        if (players.Length <= 0) {
            GameManager.Instance.allPlayers = FindObjectsOfType<PlayerController>();
            players = GameManager.Instance.allPlayers;
        }
        if (players.Length <= 0)
            return;

        int nulls = 0;
        while (TargetPlayer == null) {
            targetIndex = (targetIndex - 1 + players.Length) % players.Length;
            TargetPlayer = players[targetIndex];
            if (nulls++ >= players.Length)
                break;
        }

        TargetPlayer.cameraController.controlCamera = true;
    }
}