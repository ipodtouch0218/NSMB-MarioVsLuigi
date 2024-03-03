using UnityEngine;
using UnityEngine.UI;
using TMPro;

using NSMB.Entities.Player;
using NSMB.Extensions;
using NSMB.Game;
using NSMB.Utils;

public class UserNametag : MonoBehaviour {

    public PlayerController parent;
    private CharacterData character;

    //---Serailzied Variables
    [SerializeField] private Camera cam;
    [SerializeField] private GameObject nametag;
    [SerializeField] private TMP_Text text;
    [SerializeField] private Image arrow;
    [SerializeField] private RectTransform parentTransform;

    //---Properties
    private PlayerData Data => parent.Data;

    //---Private Variables
    private string cachedNickname;
    private NicknameColor nicknameColor;

    public void Start() {
        character = Data.GetCharacterData();
        arrow.color = parent.animationController.GlowColor;

        nicknameColor = Data.NicknameColor;
        text.color = nicknameColor.color;
    }

    public void LateUpdate() {

        // If our parent object dies, we also die.
        if (!parent) {
            Destroy(gameObject);
            return;
        }

        // If the parent disconnects, don't immediately die, but don't keep trying to update our info either.
        if (!parent.Object) {
            return;
        }

        nametag.SetActive(!(parent.IsDead && parent.IsRespawning) && GameManager.Instance.GameState >= Enums.GameState.Playing);

        if (!nametag.activeSelf) {
            return;
        }

        Vector2 worldPos = parent.animationController.models.transform.position;
        worldPos.y += parent.WorldHitboxSize.y * 1.2f + 0.5f;

        if (GameManager.Instance.loopingLevel) {
            // Wrapping
            if (Mathf.Abs(worldPos.x - cam.transform.position.x) > GameManager.Instance.LevelWidth * 0.5f) {
                worldPos.x += (cam.transform.position.x > GameManager.Instance.LevelMiddleX ? 1 : -1) * GameManager.Instance.LevelWidth;
            }
        }

        transform.position = cam.WorldToViewportPoint(worldPos, Camera.MonoOrStereoscopicEye.Mono) * parentTransform.rect.size;
        transform.position += parentTransform.position - (Vector3) (parentTransform.pivot * parentTransform.rect.size);

        if (Data && Data.Object) {
            cachedNickname ??= Data.GetNickname();

            // TODO: this allocates every frame.
            string newText = "";

            if (SessionData.Instance.Teams && Settings.Instance.GraphicsColorblind) {
                Team team = ScriptableManager.Instance.teams[Data.Team];
                newText += team.textSpriteColorblindBig;
            }
            newText += cachedNickname + "\n";

            if (parent.LivesEnabled) {
                newText += character.uistring + Utils.GetSymbolString("x" + parent.Lives + " ");
            }

            newText += Utils.GetSymbolString("Sx" + parent.Stars);

            text.text = newText;

            nicknameColor = Data.NicknameColor;
        }

        if (nicknameColor.isRainbow) {
            text.color = Utils.GetRainbowColor(parent.Runner);
        }
    }
}
