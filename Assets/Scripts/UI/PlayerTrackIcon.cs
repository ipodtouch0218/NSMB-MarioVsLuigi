using System.Collections;
using UnityEngine;
using UnityEngine.UI;

using NSMB.Entities.Player;

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
    private PlayerController playerTarget;
    private Coroutine flashRoutine;

    public void OnEnable() {
        image.enabled = true;
    }

    public void OnDisable() {
        if (flashRoutine != null) {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }
    }

    public void Start() {
        playerTarget = target.GetComponent<PlayerController>();
        if (!playerTarget) {
            Destroy(gameObject);
            return;
        }

        image.color = playerTarget.animationController.GlowColor;
        if (SessionData.Instance.Teams) {
            teamIcon.sprite = ScriptableManager.Instance.teams[playerTarget.Data.Team].spriteColorblind;
        }

        target = playerTarget.models;
    }

    public override void LateUpdate() {
        base.LateUpdate();
        transform.localScale = playerTarget.cameraController.IsControllingCamera ? FlipY : TwoThirds;
        teamIcon.gameObject.SetActive(Settings.Instance.GraphicsColorblind && SessionData.Instance.Teams && !playerTarget.cameraController.IsControllingCamera);
        allImageParent.SetActive(!HideAllPlayerIcons);

        if (flashRoutine == null && playerTarget.IsDead) {
            flashRoutine = StartCoroutine(Flash());
        }
    }

    private IEnumerator Flash() {
        while (playerTarget.IsDead) {
            image.enabled = !image.enabled;
            yield return FlashWait;
        }

        image.enabled = true;
        flashRoutine = null;
    }
}
