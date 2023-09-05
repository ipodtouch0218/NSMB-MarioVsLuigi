using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class ResizeToFit : MonoBehaviour {

    //---Private Variables
    private RectTransform rect;
    private RectTransform parent;

    public void Awake() {
        rect = GetComponent<RectTransform>();
        parent = rect.parent.GetComponent<RectTransform>();
    }

    public void LateUpdate() {
        SizeToParent();
    }

    public void SizeToParent() {
        float w = parent.rect.width;
        float h = parent.rect.height;

        if (Settings.Instance.graphicsNdsEnabled && Settings.Instance.graphicsNdsForceAspect) {
            if (Settings.Instance.graphicsNdsPixelPerfect) {
                // Resize to be pixel perfect

                RenderTexture tex = GlobalController.Instance.ndsTexture;

                // positive = N        times multiplier [1 = 1x, 2 = 2x, etc.]
                // non-pos  = 1/-(N-2) times multiplier [0 = -2 = 2 = 1/2, -1 = -3 = 3 = 1/3, etc]

                int multiplier = Mathf.Min((int) w / tex.width, (int) h / tex.height);

                if (multiplier <= 0)
                    multiplier = -(int) (Mathf.Max(tex.width / w, tex.height / h) - 1);

                (w, h) = CalculateNewWidthHeight(tex.width, tex.height, multiplier);

            } else {
                // 4:3, but scaled. Just use the parent's width and height scaled to 4:3

                if ((w / h) > (4 / 3f)) {
                    // Screen aspect ratio is bigger than 4:3, so scale based on height
                    w = h * 4f / 3f;
                } else {
                    h = w * 3f / 4f;
                }
            }
        }

        rect.sizeDelta = new(w, h);
    }

    private static (int, int) CalculateNewWidthHeight(float width, float height, int multiplier) {
        float realMultiplier = (multiplier <= 0) ? (1f / -(multiplier - 2)) : multiplier;
        return ((int) (realMultiplier * width), (int) (realMultiplier * height));
    }
}
