using UnityEngine;
using UnityEngine.UI;

public class ResizeToFit : MonoBehaviour {

    public float aspect = 4f / 3f;
    private RectTransform rect;
    private RectTransform parent;
    private Vector2 original;

    public void Start() {
        rect = GetComponent<RectTransform>();
        parent = rect.parent.GetComponent<RectTransform>();
        original = rect.sizeDelta;
    }
    public void LateUpdate() {
        if (GlobalController.Instance.settings.fourByThreeRatio && GlobalController.Instance.settings.ndsResolution)
            SizeToParent();
        else
            rect.sizeDelta = original;
    }

    public void SizeToParent(float padding = 0) {
        padding = 1 - padding;
        float w, h;
        var bounds = new Rect(0, 0, parent.rect.width, parent.rect.height);
        if (Mathf.RoundToInt(rect.eulerAngles.z) % 180 == 90)
              //Invert the bounds if the image is rotated
              bounds.size = new(bounds.height, bounds.width);
        
        //Size by height first
        h = bounds.height * padding;
        w = h * aspect;
        if (w > bounds.width * padding) { //If it doesn't fit, fallback to width;
            w = bounds.width * padding;
            h = w / aspect;
        }
        rect.sizeDelta = new(w, h);
    }

}