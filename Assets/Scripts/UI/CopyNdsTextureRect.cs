using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class CopyNdsTextureRect : MonoBehaviour {

    //---Private Variables
    private RectTransform rect, parentRect;

    public void Awake() {
        rect = GetComponent<RectTransform>();
        parentRect = GetComponentInParent<RectTransform>();
    }

    public void LateUpdate() {
        if (Settings.Instance.graphicsNdsEnabled) {
            CopyFromRect(GlobalController.Instance.ndsRect);
        } else {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
        }
    }

    private void CopyFromRect(RectTransform other) {
        rect.anchorMin = other.anchorMin;
        rect.anchorMax = other.anchorMax;
        rect.sizeDelta = other.sizeDelta;
    }
}
