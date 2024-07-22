using NSMB.Utils;
using Quantum;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PlayerTrackIcon : TrackIcon {

    //---Static Variables
    public static bool HideAllPlayerIcons = false;
    private static readonly Vector3 TwoThirds = Vector3.one * (2f / 3f);
    private static readonly Vector3 FlipY = new(1f, -1f, 1f);
    private static readonly WaitForSeconds FlashWait = new(0.1f);

    //---Serialized Variables
    [SerializeField] private GameObject allImageParent;
    [SerializeField] private Image teamIcon;

    //---Private Variables
    private Coroutine flashRoutine;

    protected override void OnEnable() {
        base.OnEnable();
        image.enabled = true;
    }

    protected override void OnDisable() {
        base.OnDisable();
        if (flashRoutine != null) {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }
    }

    public void Start() {
        var game = QuantumRunner.DefaultGame;
        var config = game.Configurations.Simulation;

        var mario = game.Frames.Predicted.Get<MarioPlayer>(targetEntity);
        image.color = Utils.GetPlayerColor(game, mario.PlayerRef);
        if (config.TeamsEnabled) {
            teamIcon.sprite = ScriptableManager.Instance.teams[mario.Team].spriteColorblind;
        }

        QuantumEvent.Subscribe<EventMarioPlayerDied>(this, OnMarioPlayerDied);
        QuantumEvent.Subscribe<EventMarioPlayerRespawned>(this, OnMarioPlayerRespawned);
    }

    public override void OnUpdateView(QuantumGame game) {
        base.OnUpdateView(game);
    }

    private IEnumerator Flash() {
        while (true) {
            image.enabled = !image.enabled;
            yield return FlashWait;
        }
    }

    public void OnMarioPlayerDied(EventMarioPlayerDied e) {
        if (e.Entity != targetEntity) {
            return;
        }

        StartCoroutine(Flash());
    }

    public void OnMarioPlayerRespawned(EventMarioPlayerRespawned e) {
        if (e.Entity != targetEntity) {
            return;
        }

        image.enabled = true;
        if (flashRoutine != null) {
            StopCoroutine(flashRoutine);
        }
        flashRoutine = null;
    }
}