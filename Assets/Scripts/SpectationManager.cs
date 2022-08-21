using System.Collections.Generic;
using UnityEngine;
using TMPro;

using NSMB.Utils;

public class SpectationManager : MonoBehaviour {

    [SerializeField] private GameObject spectationUI;
    [SerializeField] private TMP_Text spectatingText;
    private bool _spectating = false;
    public bool Spectating {
        get => _spectating;
        set {
            _spectating = value;
            if (TargetPlayer == null)
                SpectateNextPlayer();

            UpdateSpectateUI();
        }
    }
    private PlayerController _targetPlayer;
    public PlayerController TargetPlayer {
        get => _targetPlayer;
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
        if (!Spectating || !UIUpdater.Instance)
            return;

        UIUpdater.Instance.player = TargetPlayer;
        if (!TargetPlayer || !TargetPlayer.photonView)
            return;

        spectatingText.text = $"Spectating: { TargetPlayer.photonView.Owner.GetUniqueNickname() }";
    }

    public void SpectateNextPlayer() {
        if (TargetPlayer)
            TargetPlayer.cameraController.controlCamera = false;

        List<PlayerController> players = GameManager.Instance.players;
        int count = players.Count;
        if (count <= 0)
            return;

        TargetPlayer = null;

        int nulls = 0;
        while (TargetPlayer == null) {
            targetIndex = (targetIndex + 1) % count;
            TargetPlayer = players[targetIndex];
            if (nulls++ >= count)
                break;
        }

        if (TargetPlayer)
            TargetPlayer.cameraController.controlCamera = true;
    }

    public void SpectatePreviousPlayer() {
        if (TargetPlayer)
            TargetPlayer.cameraController.controlCamera = false;

        List<PlayerController> players = GameManager.Instance.players;
        int count = players.Count;
        if (count <= 0)
            return;

        TargetPlayer = null;

        int nulls = 0;
        while (TargetPlayer == null) {
            targetIndex = (targetIndex + count - 1) % count;
            TargetPlayer = players[targetIndex];
            if (nulls++ >= count)
                break;
        }

        if (TargetPlayer)
            TargetPlayer.cameraController.controlCamera = true;
    }
}