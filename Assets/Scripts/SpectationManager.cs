using UnityEngine;
using UnityEngine.InputSystem;
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
    public bool freecam;

    public void OnEnable() {
        InputSystem.controls.Spectating.PreviousTarget.started += PreviousTargetButton;
        InputSystem.controls.Spectating.NextTarget.started += NextTargetButton;
        InputSystem.controls.Spectating.PreviousTarget.canceled += PreviousTargetButton;
        InputSystem.controls.Spectating.NextTarget.canceled += NextTargetButton;
    }
    public void OnDisable() {
        InputSystem.controls.Spectating.PreviousTarget.started -= PreviousTargetButton;
        InputSystem.controls.Spectating.NextTarget.started -= NextTargetButton;
        InputSystem.controls.Spectating.PreviousTarget.canceled -= PreviousTargetButton;
        InputSystem.controls.Spectating.NextTarget.canceled -= NextTargetButton;
    }

    private void NextTargetButton(InputAction.CallbackContext obj) {
        if (!Spectating)
            return;

        if (obj.started)
            SpectateNextPlayer();
        
    }

    private void PreviousTargetButton(InputAction.CallbackContext obj) {
        if (!Spectating)
            return;

        if (obj.started)
            SpectatePreviousPlayer();
        
    }


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

        if (freecam) {
            spectatingText.text = "Freecam";
            UIUpdater.Instance.player = null;
            return;
        }

        UIUpdater.Instance.player = TargetPlayer;
        if (!TargetPlayer)
            return;
        
        spectatingText.text = $"Spectating: { TargetPlayer.photonView.Owner.NickName }";
    }
    
    public void SpectateNextPlayer() {
        if (TargetPlayer)
            TargetPlayer.cameraController.controlCamera = false;

        PlayerController[] players = GameManager.Instance.allPlayers;
        if (players.Length <= 0) {
            GameManager.Instance.allPlayers = FindObjectsOfType<PlayerController>();
            players = GameManager.Instance.allPlayers;
        }
        if (players.Length <= 0)
            return;

        TargetPlayer = null;

        if (freecam) {
            targetIndex = 0;
            freecam = false;
        }

        int nulls = 0;
        while (TargetPlayer == null) {
            targetIndex++;
            if (targetIndex >= players.Length) {
                freecam = true;
                break;
            }
            TargetPlayer = players[targetIndex];
            if (nulls++ >= players.Length)
                break;
        }

        if (TargetPlayer)
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

        if (freecam) {
            targetIndex = players.Length;
            freecam = false;
        }

        int nulls = 0;
        while (TargetPlayer == null) {
            targetIndex--;
            if (targetIndex < 0) {
                freecam = true;
                break;
            }
            TargetPlayer = players[targetIndex];
            if (nulls++ >= players.Length)
                break;
        }

        if (TargetPlayer)
            TargetPlayer.cameraController.controlCamera = true;
    }
}