using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class ResizeToFit : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private float aspect = 4f / 3f;

    //---Private Variables
    private RectTransform rect;
    private RectTransform parent;

    public void Awake() {
        rect = GetComponent<RectTransform>();
        parent = rect.parent.GetComponent<RectTransform>();
    }

    public void LateUpdate() {
        if (!Settings.Instance.graphicsNdsEnabled)
            return;

        if (Settings.Instance.graphicsNdsForceAspect)
            SizeToParent(aspect);
        else
            SizeToParent(Camera.main.aspect);
    }

    public void SizeToParent(float aspect) {
        float padding = 1;
        float w, h;
        var bounds = new Rect(0, 0, parent.rect.width, parent.rect.height);
        if (Mathf.RoundToInt(rect.eulerAngles.z) % 180 == 90)
              // Invert the bounds if the image is rotated
              bounds.size = new(bounds.height, bounds.width);

        // Size by height first
        h = bounds.height * padding;
        w = h * aspect;
        if (w > bounds.width * padding) { // If it doesn't fit, fallback to width;
            w = bounds.width * padding;
            h = w / aspect;
        }

        if (Settings.Instance.graphicsNdsPixelPerfect && Settings.Instance.graphicsNdsForceAspect) {
            // Resize to be pixel perfect
            RenderTexture tex = GlobalController.Instance.ndsTexture;
            float multiplier = Mathf.Min((int) w / tex.width, (int) h / tex.height);
            if (multiplier <= 0) {
                // Shoot
                multiplier = 1f / (int) (Mathf.Max(tex.width / w, tex.height / h) + 1);
            }
            w = multiplier * tex.width;
            h = multiplier * tex.height;
        }

        rect.sizeDelta = new(w, h);
    }
}
