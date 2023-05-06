using System.Collections;
using UnityEngine;

public class PlayerTrackIcon : TrackIcon {

    //---Static Variables
    private static readonly Vector3 TwoThirds = Vector3.one * (2f / 3f);
    private static readonly Vector3 FlipY = new(1f, -1f, 1f);
    private static readonly WaitForSeconds FlashWait = new(0.1f);

    //---Private Variables
    private PlayerController playerTarget;
    private Coroutine flashRoutine;

    public void Start() {
        playerTarget = target.GetComponent<PlayerController>();
        if (!playerTarget) {
            Destroy(gameObject);
            return;
        }

        image.color = playerTarget.animationController.GlowColor;
        target = playerTarget.models;
    }

    public override void LateUpdate() {
        base.LateUpdate();
        transform.localScale = playerTarget.cameraController.IsControllingCamera ? FlipY : TwoThirds;

        if (flashRoutine == null && playerTarget.IsDead)
            flashRoutine = StartCoroutine(Flash());
    }

    private IEnumerator Flash() {
        bool visible = true;
        while (playerTarget.IsDead) {
            visible = !visible;
            image.enabled = visible;
            yield return FlashWait;
        }

        image.enabled = true;
        flashRoutine = null;
    }
}
