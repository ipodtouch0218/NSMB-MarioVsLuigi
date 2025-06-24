using UnityEngine;

namespace NSMB.UI.Elements {
    [ExecuteAlways, RequireComponent(typeof(RectTransform))]
    public class ScaleWithParent : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private float targetWidth = 1000, aspectRatio = (16f/9f);

        //---Private Variables
        private DrivenRectTransformTracker tracker;
#if BROKEN_VERSION
    private int width, height;
#endif

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

#if BROKEN_VERSION
    public void LateUpdate() {
        if (Screen.width != width || Screen.height != height) {
            OnRectTransformDimensionsChange();
            width = Screen.width;
            height = Screen.height;
        }

        Transform tf = transform;
        do {
            if (tf.hasChanged) {
                Debug.Log("tf has changed; " + tf.name);
                OnRectTransformDimensionsChange();
                tf.hasChanged = false;
                break;
            }
        } while (tf = tf.parent);
    }

    public void OnTransformParentChanged() {
        OnRectTransformDimensionsChange();
    }
#else
        public void LateUpdate() {
            // I can't get this shit to work... it SHOULD only
            // get called when the parent changes, but...
            // just fucking run every frame, i don't give a shit
            // at this point.
            OnRectTransformDimensionsChange();
        }
#endif

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
}
