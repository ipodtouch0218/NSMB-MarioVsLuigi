using UnityEngine;
using UnityEngine.UI;
using TMPro;

using NSMB.Extensions;
using NSMB.Utils;

public class UserNametag : MonoBehaviour {

    public PlayerController parent;
    private PlayerData data;
    private CharacterData character;

    //---Serailzied Variables
    [SerializeField] private Camera cam;
    [SerializeField] private GameObject nametag;
    [SerializeField] private TMP_Text text;
    [SerializeField] private Image arrow;

    private string cachedNickname;
    private bool rainbowName;

    public void Start() {
        rainbowName = parent.Object.InputAuthority.HasRainbowName();
        data = parent.Object.InputAuthority.GetPlayerData(parent.Runner);
        character = data.GetCharacterData();
        arrow.color = parent.animationController.GlowColor;
    }

    public void LateUpdate() {
        if (!parent) {
            Destroy(gameObject);
            return;
        }

        nametag.SetActive(!(parent.IsDead && parent.IsRespawning));

        Vector2 worldPos = parent.animationController.models.transform.position;
        worldPos.y += parent.WorldHitboxSize.y * 1.2f + 0.5f;

        if (GameManager.Instance.loopingLevel && Mathf.Abs(cam.transform.position.x - worldPos.x) > GameManager.Instance.levelWidthTile * (1 / 4f))
            worldPos.x += Mathf.Sign(cam.transform.position.x) * GameManager.Instance.levelWidthTile / 2f;

        Vector2 size = new(Screen.width, Screen.height);
        Vector3 screenPoint = cam.WorldToViewportPoint(worldPos, Camera.MonoOrStereoscopicEye.Mono) * size;
        screenPoint.z = 0;

        if (Settings.Instance.ndsResolution && Settings.Instance.fourByThreeRatio) {
            // handle black borders
            float screenW = Screen.width;
            float screenH = Screen.height;
            float screenAspect = screenW / screenH;

            if (screenAspect > cam.aspect) {
                float availableWidth = screenH * cam.aspect;
                float widthPercentage = availableWidth / screenW;

                screenPoint.x *= widthPercentage;
                screenPoint.x += (screenW - availableWidth) / 2;
            } else {
                float availableHeight = screenW * (1f / cam.aspect);
                float heightPercentage = availableHeight / screenH;
                screenPoint.y *= heightPercentage;
                screenPoint.y += (screenH - availableHeight) / 2;

                screenPoint.x *= heightPercentage;
            }
        }
        transform.position = screenPoint;

        cachedNickname ??= data.GetNickname();

        text.text = (data.IsRoomOwner ? "<sprite=5>" : "") + cachedNickname + "\n";

        if (parent.Lives >= 0)
            text.text += character.uistring + Utils.GetSymbolString("x" + parent.Lives + " ");

        text.text += Utils.GetSymbolString("Sx" + parent.Stars);

        if (rainbowName)
            text.color = Utils.GetRainbowColor(parent.Runner);
    }
}
