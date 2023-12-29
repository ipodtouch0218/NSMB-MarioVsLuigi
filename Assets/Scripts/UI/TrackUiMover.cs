using UnityEngine;

public class TrackUiMover : MonoBehaviour {

    [SerializeField] private float yPosNormal = -14, yPosColorblind = -19;
    [SerializeField] private RectTransform rectTransform;

    public void OnValidate() {
        if (!rectTransform) rectTransform = GetComponent<RectTransform>();
    }

    public void OnEnable() {
        Settings.OnColorblindModeChanged += OnColorblindModeChanged;
        OnColorblindModeChanged();
    }

    public void OnDisable() {
        Settings.OnColorblindModeChanged -= OnColorblindModeChanged;
    }

    private void OnColorblindModeChanged() {
        rectTransform.anchoredPosition = new(rectTransform.anchoredPosition.x,
            (SessionData.Instance && SessionData.Instance.Teams && Settings.Instance.GraphicsColorblind) ? yPosColorblind : yPosNormal);
    }
}
