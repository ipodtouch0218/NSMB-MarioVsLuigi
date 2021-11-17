using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class PipeButton : MonoBehaviour {

    private Button button;
    private RectTransform rect;
    private Vector2 anchor;
    void Start() {
        rect = GetComponent<RectTransform>();
        button = GetComponent<Button>();
        anchor = rect.anchorMin;
    }

    void Update() {
        if (EventSystem.current.currentSelectedGameObject == gameObject) {
            rect.anchorMin = anchor;
        } else {
            rect.anchorMin = anchor + new Vector2(0.1f,0);
        }
    }
}
