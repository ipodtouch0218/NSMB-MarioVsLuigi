using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
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
            if (_targetPlayer)
                _targetPlayer.cameraController.IsControllingCamera = false;

            _targetPlayer = value;
            if (value != null) {
                UpdateSpectateUI();
                value.cameraController.IsControllingCamera = true;
            }
        }
    }
    private int targetIndex;

    public void OnEnable() {
        InputSystem.controls.UI.SpectatePlayerByIndex.performed += SpectatePlayerIndex;
    }

    public void OnDisable() {
        InputSystem.controls.UI.SpectatePlayerByIndex.performed -= SpectatePlayerIndex;
    }

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
        List<PlayerController> players = GameManager.Instance.players;
        int count = players.Count;
        if (count <= 0)
            return;

        TargetPlayer = null;

        int nulls = 0;
        while (!TargetPlayer) {
            targetIndex = (targetIndex + 1) % count;
            TargetPlayer = players[targetIndex];
            if (nulls++ >= count)
                break;
        }
    }

    public void SpectatePreviousPlayer() {
        List<PlayerController> players = GameManager.Instance.players;
        int count = players.Count;
        if (count <= 0)
            return;

        TargetPlayer = null;

        int nulls = 0;
        while (!TargetPlayer) {
            targetIndex = (targetIndex + count - 1) % count;
            TargetPlayer = players[targetIndex];
            if (nulls++ >= count)
                break;
        }
    }

    private void SpectatePlayerIndex(InputAction.CallbackContext context) {
        if (!Spectating)
            return;

        if (int.TryParse(context.control.name, out int index)) {
            index += 9;
            index %= 10;

            List<PlayerController> sortedPlayers = new(GameManager.Instance.players);
            sortedPlayers.Sort(new PlayerComparer());

            if (index >= sortedPlayers.Count)
                return;

            PlayerController newTarget = sortedPlayers[index];

            if (!newTarget)
                return;

            TargetPlayer = newTarget;
        }
    }

    public class PlayerComparer : IComparer<PlayerController> {
        public int Compare(PlayerController x, PlayerController y) {
            if (!x ^ !y)
                return !x ? 1 : -1;

            if (x.stars == y.stars || x.lives == 0 || y.lives == 0) {
                if (Mathf.Max(0, x.lives) == Mathf.Max(0, y.lives))
                    return x.playerId - y.playerId;

                return y.lives - x.lives;
            }

            return y.stars - x.stars;
        }
    }
}