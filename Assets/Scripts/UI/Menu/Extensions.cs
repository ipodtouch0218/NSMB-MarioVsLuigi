using Fusion;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.Extensions {
    public static class UnityExtensions {

        private static readonly Vector3[] corners = new Vector3[4];

        public static Bounds TransformBoundsTo(this RectTransform source, Transform target) {
            // Based on code in ScrollRect's internal GetBounds and InternalGetBounds methods
            var bounds = new Bounds();
            if (source != null) {
                source.GetWorldCorners(corners);

                var vMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                var vMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);

                var matrix = target.worldToLocalMatrix;
                for (int j = 0; j < 4; j++) {
                    Vector3 v = matrix.MultiplyPoint3x4(corners[j]);
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

            if (onlyOffscreen && (elementBounds.center.y + elementBounds.extents.y) < 0 && (elementBounds.center.y - elementBounds.extents.y) > -viewRect.height)
                return scrollRect.verticalNormalizedPosition;

            // Normalize and apply the calculated offset
            var scrollPos = scrollRect.verticalNormalizedPosition - scrollRect.NormalizeScrollDistance(1, offset);
            return Mathf.Clamp(scrollPos, 0f, 1f);
        }

        //Missing multiply and divide operators for vector3
        public static Vector3 Multiply(this Vector3 a, Vector3 b) {
            return new(a.x * b.x, a.y * b.y, a.z * b.z);
        }

        public static Vector3 Divide(this Vector3 a, Vector3 b) {
            return new(a.x / b.x, a.y / b.y, a.z / b.z);
        }

        //easy sound clips
        public static void PlayOneShot(this AudioSource source, Enums.Sounds clip, CharacterData character = null, byte variant = 0, float volume = 1f) {
            source.PlayOneShot(clip.GetClip(character, variant), volume);
        }
    }
}
