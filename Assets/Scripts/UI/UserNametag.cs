using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UserNametag : MonoBehaviour {

    [SerializeField] new Camera camera;
    private RectTransform parentCanvas;
    public GameObject nametag;
    public TMP_Text text;
    public Image arrow;
    public PlayerController parent;
    public void Start() {
        parentCanvas = transform.parent.GetComponent<RectTransform>();
    }

    void LateUpdate() {
        if (parent == null) {
            Destroy(gameObject);
            return;
        }

        arrow.color = parent.AnimationController.GlowColor;
        nametag.SetActive(parent.spawned);

        Vector2 worldPos = new(parent.transform.position.x, parent.transform.position.y + (parent.hitboxes[0].size.y * parent.transform.lossyScale.y * 1.2f) + 0.5f);
        if (Mathf.Abs(camera.transform.position.x - worldPos.x) > GameManager.Instance.levelWidthTile * (1 / 4f))
            worldPos.x += Mathf.Sign(camera.transform.position.x) * GameManager.Instance.levelWidthTile / 2f;

        Rect t = parentCanvas.rect;
        Vector2 size = new(t.size.y * camera.aspect, t.size.y);
        Vector3 screenPoint = camera.WorldToViewportPoint(worldPos, Camera.MonoOrStereoscopicEye.Mono) * size;
        screenPoint.z = 0;

        if (GlobalController.Instance.settings.ndsResolution && GlobalController.Instance.settings.fourByThreeRatio) {
            // handle black borders
            float screenW = Screen.width;
            float screenH = Screen.height;
            float screenAspect = screenW / screenH;

            if (screenAspect > camera.aspect) {
                screenPoint.x += (screenW - (screenH * camera.aspect)) / 2;
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
