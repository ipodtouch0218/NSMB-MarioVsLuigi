using UnityEngine;
using UnityEngine.EventSystems;

[ExecuteAlways, RequireComponent(typeof(RectTransform))]
public class ScaleWithParent : UIBehaviour {

    [SerializeField] private float targetWidth = 1000, aspectRatio = (16f/9f);

    private DrivenRectTransformTracker tracker;

    protected override void OnEnable() {
        tracker.Add(this, (RectTransform) transform, DrivenTransformProperties.Scale | DrivenTransformProperties.Anchors | DrivenTransformProperties.SizeDelta | DrivenTransformProperties.AnchoredPosition);
        OnRectTransformDimensionsChange();
    }

    protected override void OnDisable() {
        tracker.Clear();
    }

    protected override void OnRectTransformDimensionsChange() {
        RectTransform rt = (RectTransform) transform;
        RectTransform parent = (RectTransform) rt.parent;

        rt.anchorMin = new(0, 0.5f);
        rt.anchorMax = new(1, 0.5f);

        float scale = Mathf.Min(1, parent.rect.size.x / targetWidth);
        if (scale <= 0) {
            // Avoid NaNs
            return;
        }
        rt.localScale = Vector3.one * scale;

        float differenceX = Mathf.Max(0, targetWidth - parent.rect.size.x);
        rt.sizeDelta = new(
            differenceX,
            parent.rect.size.y / scale
        );
    }
}
