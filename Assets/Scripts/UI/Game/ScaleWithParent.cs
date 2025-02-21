using UnityEngine;
using UnityEngine.EventSystems;

[ExecuteAlways, RequireComponent(typeof(RectTransform))]
public class ScaleWithParent : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private float targetWidth = 1000, aspectRatio = (16f/9f);

    //---Private Variables
    private DrivenRectTransformTracker tracker;
    private int width, height;

#if UNITY_EDITOR
    public void OnValidate() {
        ValidationUtility.SafeOnValidate(() => {
            if (!this) {
                return;
            }
            OnRectTransformDimensionsChange();
        });
    }
#endif

    public void OnEnable() {
        tracker.Add(this, (RectTransform) transform, DrivenTransformProperties.Scale | DrivenTransformProperties.Anchors | DrivenTransformProperties.SizeDelta | DrivenTransformProperties.AnchoredPosition);
        OnRectTransformDimensionsChange();
    }

    public void OnDisable() {
        tracker.Clear();
    }

    public void Update() {
        if (Screen.width != width || Screen.height != height) {
            OnRectTransformDimensionsChange();
            width = Screen.width;
            height = Screen.height;
        }

        Transform tf = transform;
        do {
            if (tf.hasChanged) {
                OnRectTransformDimensionsChange();
                tf.hasChanged = false;
                break;
            }
        } while (tf = tf.parent);
    }

    public void OnTransformParentChanged() {
        OnRectTransformDimensionsChange();
    }

    public void OnRectTransformDimensionsChange() {
        RectTransform rt = (RectTransform) transform;
        RectTransform parent = (RectTransform) rt.parent;

        rt.anchorMin = new(0.5f, 0.5f);
        rt.anchorMax = new(0.5f, 0.5f);

        float scale = parent.rect.size.x / targetWidth;
        if (scale == 0) { 
            return;
        }

        if (scale > 1) {
            scale = 1;
            float expectedHeight = targetWidth / aspectRatio;
            if (parent.rect.size.y < expectedHeight) {
                // Can't fit; we still need to shrink.
                scale = parent.rect.size.y / expectedHeight;
            }

            // Screen too wide. Expand.
            rt.localScale = Vector3.one * scale;
            rt.sizeDelta = new(
                parent.rect.size.x / scale,
                parent.rect.size.y / scale
            );
        } else {
            rt.localScale = Vector3.one * scale;
            rt.sizeDelta = new(
                targetWidth,
                parent.rect.size.y / scale
            );
        }
    }
}
