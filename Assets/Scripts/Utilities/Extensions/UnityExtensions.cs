using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.Utilities.Extensions {
    public static class UnityExtensions {

        private static readonly Vector3[] CornerBuffer = new Vector3[4];

        public static Bounds TransformBoundsTo(this RectTransform source, Transform target) {
            // Based on code in ScrollRect's internal GetBounds and InternalGetBounds methods
            var bounds = new Bounds();
            if (source != null) {
                source.GetWorldCorners(CornerBuffer);

                var vMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                var vMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);

                var matrix = target.worldToLocalMatrix;
                for (int j = 0; j < 4; j++) {
                    Vector3 v = matrix.MultiplyPoint3x4(CornerBuffer[j]);
                    vMin = Vector3.Min(v, vMin);
                    vMax = Vector3.Max(v, vMax);
                }

                bounds = new Bounds(vMin, Vector3.zero);
                bounds.Encapsulate(vMax);
            }
            return bounds;
        }

        public static float NormalizeScrollDistance(this ScrollRect scrollRect, int axis, float distance) {
            // Based on code in ScrollRect's internal SetNormalizedPosition method
            var viewport = scrollRect.viewport;
            var viewRect = viewport != null ? viewport : scrollRect.GetComponent<RectTransform>();
            var viewBounds = new Bounds(viewRect.rect.center, viewRect.rect.size);

            var content = scrollRect.content;
            var contentBounds = content != null ? content.TransformBoundsTo(viewRect) : new Bounds();

            var hiddenLength = contentBounds.size[axis] - viewBounds.size[axis];
            return distance / hiddenLength;
        }

        public static float ScrollToCenter(this ScrollRect scrollRect, RectTransform target, bool onlyOffscreen) {
            // The scroll rect's view's space is used to calculate scroll position
            var view = scrollRect.viewport != null ? scrollRect.viewport : scrollRect.GetComponent<RectTransform>();

            // Calcualte the scroll offset in the view's space
            var viewRect = view.rect;
            var elementBounds = target.TransformBoundsTo(view);
            var offset = viewRect.center.y - elementBounds.center.y;

            if (onlyOffscreen && (elementBounds.center.y + elementBounds.extents.y) < 0 && (elementBounds.center.y - elementBounds.extents.y) > -viewRect.height) {
                return scrollRect.verticalNormalizedPosition;
            }

            // Normalize and apply the calculated offset
            var scrollPos = scrollRect.verticalNormalizedPosition - scrollRect.NormalizeScrollDistance(1, offset);
            return Mathf.Clamp(scrollPos, 0f, 1f);
        }

        public static float ScrollIntoView(this ScrollRect scrollRect, RectTransform target, bool onlyOffscreen, float additionalOffset) {
            // The scroll rect's view's space is used to calculate scroll position
            var view = scrollRect.viewport != null ? scrollRect.viewport : scrollRect.GetComponent<RectTransform>();

            // Calcualte the scroll offset in the view's space
            var viewRect = view.rect;
            var elementBounds = target.TransformBoundsTo(view);

            var offset = viewRect.center.y - elementBounds.center.y;
            if (offset < 0) {
                // Top bound
                offset -= elementBounds.extents.y;
            } else {
                // Bottom bound
                offset += elementBounds.extents.y;
            }

            if (onlyOffscreen && Mathf.Abs(offset) < (viewRect.height * 0.5f) - additionalOffset) {
                return scrollRect.verticalNormalizedPosition;
            }

            offset += Mathf.Sign(offset) * additionalOffset;
            offset -= Mathf.Sign(offset) * viewRect.height * 0.5f;

            // Normalize and apply the calculated offset
            var scrollPos = scrollRect.verticalNormalizedPosition - scrollRect.NormalizeScrollDistance(1, offset);
            return Mathf.Clamp(scrollPos, 0f, 1f);
        }

        // Missing component-wise functions for vector3
        public static Vector3 Multiply(this Vector3 a, Vector3 b) {
            return new(a.x * b.x, a.y * b.y, a.z * b.z);
        }

        public static Vector3 Divide(this Vector3 a, Vector3 b) {
            return new(a.x / b.x, a.y / b.y, a.z / b.z);
        }

        //easy sound clips
        public static void PlayOneShot(this AudioSource source, SoundEffect clip, CharacterAsset character = null, int variant = 0, float volume = 1f) {
            source.PlayOneShot(clip.GetClip(character, variant), volume);
        }

        public static void PlayOneShot(this AudioSource source, SoundEffectDataAttribute data, CharacterAsset character = null, int variant = 0, float volume = 1f) {
            source.PlayOneShot(data.GetClip(character, variant), volume);
        }

        public static void SetLossyScale(this Transform transform, Vector3 lossyScale) {
            if (transform.parent) {
                lossyScale = lossyScale.Divide(transform.parent.lossyScale);
            }

            if (lossyScale.z == 0 || !float.IsFinite(lossyScale.z)) {
                lossyScale.z = 1;
            }

            transform.localScale = lossyScale;
        }

        public static void SetIfNull<T>(this Component component, ref T var, GetComponentType children = GetComponentType.Self) where T : Component {
            if (component && !var) {
                var = children switch {
                    GetComponentType.Children => component.GetComponentInChildren<T>(),
                    GetComponentType.Parent => component.GetComponentInParent<T>(),
                    _ => component.GetComponent<T>(),
                };
            }
        }

        public static void SetIfNull<T>(this Component component, ref T[] var, GetComponentType children = GetComponentType.Self) where T : Component {
            if (component && (var == null || var.Length <= 0)) {
                var = children switch {
                    GetComponentType.Children => component.GetComponentsInChildren<T>(),
                    GetComponentType.Parent => component.GetComponentsInParent<T>(),
                    _ => component.GetComponents<T>(),
                };
            }
        }

        public static void StopCoroutineNullable(this MonoBehaviour b, ref Coroutine coroutine) {
            if (b == null || coroutine == null) {
                return;
            }

            b.StopCoroutine(coroutine);
            coroutine = null;
        }

        public enum GetComponentType {
            Self,
            Parent,
            Children
        }

        public static void SetTextIfDifferent(this TMP_Text text, string newText) {
            if (!text.text.Equals(newText)) {
                text.text = newText;
            }
        }

        public static void SetHorizontalAlignmentIfDifferent(this TMP_Text text, HorizontalAlignmentOptions alignment) {
            if (text.horizontalAlignment != alignment) {
                text.horizontalAlignment = alignment;
            }
        }
    }
}
