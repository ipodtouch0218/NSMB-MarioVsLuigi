using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.Extensions {
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

        // Missing component-wise multiply and divide operators for vector3
        public static Vector3 Multiply(this Vector3 a, Vector3 b) {
            return new(a.x * b.x, a.y * b.y, a.z * b.z);
        }

        public static Vector3 Divide(this Vector3 a, Vector3 b) {
            return new(a.x / b.x, a.y / b.y, a.z / b.z);
        }

        /// <summary>
        /// Sets the x, y, and z values for an existing Vector3 if the given x, y, and z values are not null.
        /// </summary>
        /// <param name="vec">An existing Vector3 to modify</param>
        /// <param name="x">The new X value. If null, the existing X value is kept.</param>
        /// <param name="y">The new Y value. If null, the existing Y value is kept.</param>
        /// <param name="z">The new Z value. If null, the existing Z value is kept.</param>
        public static void SetNonNulls(this ref Vector3 vec, float? x, float? y, float? z) {
            vec.x = x ?? vec.x;
            vec.y = y ?? vec.y;
            vec.z = z ?? vec.z;
        }

        //easy sound clips
        public static void PlayOneShot(this AudioSource source, SoundEffect clip, CharacterAsset character = null, byte variant = 0, float volume = 1f) {
            source.PlayOneShot(clip.GetClip(character, variant), volume);
        }

        public static void AddRange<K, V>(this Dictionary<K, V> dict, Dictionary<K, V> adder) {
            foreach ((K key, V value) in adder) {
                dict[key] = value;
            }
        }

        public static bool ContainsLayer(this LayerMask mask, int layer) {
            return (mask & (1 << layer)) != 0;
        }

        public static bool ContainsLayer(this LayerMask mask, string layerName) {
            return ContainsLayer(mask, LayerMask.NameToLayer(layerName));
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

        public enum GetComponentType {
            Self,
            Parent,
            Children
        }
    }
}
