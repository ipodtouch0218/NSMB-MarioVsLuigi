using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NSMB.Utils;

public class UserNametag : MonoBehaviour {

    [SerializeField] new Camera camera;
    public GameObject nametag;
    public TMP_Text text;
    public Image arrow;
    public PlayerController parent;

    public void LateUpdate() {
        if (parent == null) {
            Destroy(gameObject);
            return;
        }

        arrow.color = parent.AnimationController.GlowColor;
        nametag.SetActive(parent.spawned);

        Vector2 worldPos = new(parent.transform.position.x, parent.transform.position.y + (parent.WorldHitboxSize.y * 1.2f) + 0.5f);
        if (GameManager.Instance.loopingLevel && Mathf.Abs(camera.transform.position.x - worldPos.x) > GameManager.Instance.levelWidthTile * (1 / 4f))
            worldPos.x += Mathf.Sign(camera.transform.position.x) * GameManager.Instance.levelWidthTile / 2f;

        Vector2 size = new(Screen.width, Screen.height);
        Vector3 screenPoint = camera.WorldToViewportPoint(worldPos, Camera.MonoOrStereoscopicEye.Mono) * size;
        screenPoint.z = 0;

        if (GlobalController.Instance.settings.ndsResolution && GlobalController.Instance.settings.fourByThreeRatio) {
            // handle black borders
            float screenW = Screen.width;
            float screenH = Screen.height;
            float screenAspect = screenW / screenH;

            if (screenAspect > camera.aspect) {
                float availableWidth = screenH * camera.aspect;
                float widthPercentage = availableWidth / screenW;

                screenPoint.x *= widthPercentage;
                screenPoint.x += (screenW - availableWidth) / 2;
            } else {
                float availableHeight = screenW * (1f / camera.aspect);
                float heightPercentage = availableHeight / screenH;
                screenPoint.y *= heightPercentage;
                screenPoint.y += (screenH - availableHeight) / 2;

                screenPoint.x *= heightPercentage;
            }
        }
        transform.position = screenPoint;

        text.text = (parent.photonView.Owner.IsMasterClient ? "<sprite=5>" : "") + parent.photonView.Owner.NickName;

        text.text += "\n";
        if (parent.lives >= 0)
            text.text += Utils.GetCharacterData(parent.photonView.Owner).uistring + Utils.GetSymbolString($"x{parent.lives} ");

        text.text += Utils.GetSymbolString($"Sx{parent.stars}");
    }
}
