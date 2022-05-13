using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class UserNametag : MonoBehaviour {

    private RectTransform parentCanvas;
    public TMP_Text text;
    public PlayerController parent;
    public void Start() {
        parentCanvas = transform.parent.GetComponent<RectTransform>();
    }

    void Update() {
        if (parent == null) {
            Destroy(gameObject);
            return;
        }

        text.enabled = !parent.dead;

        Vector2 worldPos = new(parent.transform.position.x, parent.transform.position.y + (parent.hitboxes[0].size.y * parent.transform.lossyScale.y * 1.2f) + 0.2f);
        if (GameManager.Instance.loopingLevel && Mathf.Abs(worldPos.x - Camera.main.transform.position.x) > GameManager.Instance.levelWidthTile / 4f)
            worldPos.x -= GameManager.Instance.levelWidthTile / 2f * Mathf.Sign(worldPos.x - Camera.main.transform.position.x);

        transform.position = Camera.main.WorldToViewportPoint(worldPos, Camera.MonoOrStereoscopicEye.Mono) * parentCanvas.rect.size;

        text.text = (parent.photonView.Owner.IsMasterClient ? "<sprite=5>" : "") + parent.photonView.Owner.NickName;

        text.text += "\n";
        if (parent.lives >= 0)
            text.text += Utils.GetCharacterData(parent.photonView.Owner).uistring + Utils.GetSymbolString($"x{parent.lives} ");

        text.text += Utils.GetSymbolString($"Sx{parent.stars}");
    }
}
