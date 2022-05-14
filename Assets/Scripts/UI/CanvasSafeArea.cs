using UnityEngine;

public class CanvasSafeArea : MonoBehaviour
{
    public Canvas canvas;
    RectTransform safeArea;

    Rect currentSafeArea = new Rect();
    ScreenOrientation screenOrientation = ScreenOrientation.AutoRotation;

    void Start()
    {
        safeArea = GetComponent<RectTransform>();

        screenOrientation = Screen.orientation;
        currentSafeArea = Screen.safeArea;

        UpdateSafeArea();
    }

    void Update()
    {
        if (screenOrientation != Screen.orientation || currentSafeArea != Screen.safeArea)
        {
            UpdateSafeArea();
        }
    }

    void UpdateSafeArea()
    {
        if (safeArea == null)
            return;

        Rect area = Screen.safeArea;

        Vector2 anchorMin = area.position;
        Vector2 anchorMax = area.position + area.size;

        anchorMin.x /= canvas.pixelRect.width;
        anchorMin.y /= canvas.pixelRect.height;

        anchorMax.x /= canvas.pixelRect.width;
        anchorMax.y /= canvas.pixelRect.height;

        safeArea.anchorMin = anchorMin;
        safeArea.anchorMax = anchorMax;

        screenOrientation = Screen.orientation;
        currentSafeArea = area;
    }
}
