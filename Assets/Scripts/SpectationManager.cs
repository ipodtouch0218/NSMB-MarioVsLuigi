using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

using Fusion;
using NSMB.Extensions;
using NSMB.Translation;

public class SpectationManager : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private GameObject spectationUI;
    [SerializeField] private TMP_Text spectatingText;

    //---Public Properties
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

    //---Private Variables
    private int targetIndex;

    public void OnEnable() {
        ControlSystem.controls.UI.SpectatePlayerByIndex.performed += SpectatePlayerIndex;
        GlobalController.Instance.translationManager.OnLanguageChanged += OnLanguageChanged;
        OnLanguageChanged(GlobalController.Instance.translationManager);
    }

    public void OnDisable() {
        ControlSystem.controls.UI.SpectatePlayerByIndex.performed -= SpectatePlayerIndex;
        GlobalController.Instance.translationManager.OnLanguageChanged -= OnLanguageChanged;
    }

    public void Update() {
        if (!Spectating)
            return;

        if (!TargetPlayer)
            SpectateNextPlayer();
        else
            TargetPlayer.cameraController.IsControllingCamera = true;
    }

    public void UpdateSpectateUI() {
        spectationUI.SetActive(Spectating);
        if (!Spectating || !UIUpdater.Instance)
            return;

        UIUpdater.Instance.player = TargetPlayer;
        if (!TargetPlayer)
            return;

        string username = TargetPlayer.Object.InputAuthority.GetPlayerData(TargetPlayer.Runner).GetNickname();
        spectatingText.text = GlobalController.Instance.translationManager.GetTranslationWithReplacements("ui.game.spectating", "playername", username);
    }

    public void SpectateNextPlayer() {
        NetworkLinkedList<PlayerController> players = GameManager.Instance.AlivePlayers;
        int count = players.Count;
        if (count <= 0)
            return;

        TargetPlayer = null;

        int nulls = 0;
        while (!TargetPlayer) {
            targetIndex = (targetIndex + 1) % count;
            TargetPlayer = players[targetIndex];
            if (nulls++ > count)
                break;
        }
    }

    public void SpectatePreviousPlayer() {
        NetworkLinkedList<PlayerController> players = GameManager.Instance.AlivePlayers;
        int count = players.Count;
        if (count <= 0)
            return;

        TargetPlayer = null;

        int nulls = 0;
        while (!TargetPlayer) {
            targetIndex = (targetIndex + count - 1) % count;
            TargetPlayer = players[targetIndex];
            if (nulls++ > count)
                break;
        }
    }

    private void SpectatePlayerIndex(InputAction.CallbackContext context) {
        if (!Spectating)
            return;

        if (int.TryParse(context.control.name, out int index)) {
            index += 9;
            index %= 10;

            List<PlayerController> sortedPlayers = new(GameManager.Instance.AlivePlayers);
            sortedPlayers.Sort(new PlayerComparer());

            if (index >= sortedPlayers.Count)
                return;

            PlayerController newTarget = sortedPlayers[index];

            if (!newTarget)
                return;

            TargetPlayer = newTarget;
        }
    }

    private void OnLanguageChanged(TranslationManager tm) {
        UpdateSpectateUI();
    }

    public class PlayerComparer : IComparer<PlayerController> {
        public int Compare(PlayerController x, PlayerController y) {
            if (!x ^ !y)
                return !x ? 1 : -1;

            if (x.Stars == y.Stars || x.Lives == 0 || y.Lives == 0) {
                if (Mathf.Max(0, x.Lives) == Mathf.Max(0, y.Lives))
                    return x.PlayerId - y.PlayerId;

                return y.Lives - x.Lives;
            }

            return y.Stars - x.Stars;
        }
    }
}
