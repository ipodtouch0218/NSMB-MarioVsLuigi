using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class PipeButton : MonoBehaviour {

    public Color selectedColor = Color.white, deselectedColor = Color.gray;
    private Color disabledColor;
    private Button button;
    private Image image;
    private RectTransform rect;
    private Vector2 anchor, adjustedAnchor;
    
    void Start() {
        rect = GetComponent<RectTransform>();
        button = GetComponent<Button>();
        image = GetComponent<Image>();
        anchor = rect.anchorMin;
        adjustedAnchor = anchor + new Vector2(0.1f,0);
        disabledColor = new Color(deselectedColor.r, deselectedColor.g, deselectedColor.b, deselectedColor.a/2f);
    }

    void Update() {
        if (!button.interactable) {
            rect.anchorMin = adjustedAnchor;
            image.color = disabledColor;
            return;
        }
        if (EventSystem.current.currentSelectedGameObject == gameObject) {
            rect.anchorMin = anchor;
            image.color = selectedColor;
        } else {
            rect.anchorMin = adjustedAnchor;
            image.color = deselectedColor;
        }
    }
}
