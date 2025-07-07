using NSMB.Utilities.Extensions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu {
    public class SelectionCursor : MonoBehaviour {

        //---Static Variables
        private static readonly Vector3[] cornerBuffer = new Vector3[4];

        //---Serialized Variables
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private Image image;
        [SerializeField] private Vector2 sizeIncrease = new Vector2(16, 16);
        [SerializeField] private Color minOpacity;
        [SerializeField] private float maxMovementDistance = 100;
        [SerializeField] private bool quickSnap = true;

        //---Private Variables
        private GameObject old;
        private bool hidden = true;
        private Vector2 sizeVelocity;
        private Vector3 positionVelocity;
        private float fadeValue;

        public void OnValidate() {
            this.SetIfNull(ref rectTransform);
            this.SetIfNull(ref image);
        }

        public void Update() {
            GameObject current = EventSystem.current.currentSelectedGameObject;
            bool newObject = false;
            if (current != old) {
                fadeValue = 2f;
                old = current;
                /* this works. but if the parent gets destroyed we lose the cursor forever. bummer.
                if (current) {
                    if (current.transform.parent) {
                        rectTransform.SetParent(current.transform.parent);
                    } else {
                        rectTransform.SetParent(current.transform);
                    }
                }
                */
                newObject = true;
            }

            if (!current
                || !current.activeInHierarchy
                || current.layer == LayerMask.NameToLayer("UINoCursor")
                || current.layer == LayerMask.NameToLayer("Default")
                || (current.TryGetComponent(out Image currentImage) && currentImage.color.r == 0 && currentImage.color.a == 0)) {
                image.enabled = false;
                hidden = true;
                return;
            }

            RectTransform currentRectTransform = current.GetComponent<RectTransform>();
            currentRectTransform.GetWorldCorners(cornerBuffer);

            Vector3 targetPosition = (cornerBuffer[0] + cornerBuffer[2]) / 2;
            Vector2 targetSize = new Vector2(Mathf.Abs(cornerBuffer[2].x - cornerBuffer[0].x), Mathf.Abs(cornerBuffer[2].y - cornerBuffer[0].y)) + sizeIncrease;

            if (hidden) {
                image.enabled = true;
                image.color = Color.white;
                rectTransform.position = targetPosition;
                rectTransform.sizeDelta = targetSize;
                positionVelocity = default;
                sizeVelocity = default;
                fadeValue = 2f;
                hidden = false;
                newObject = false;
            }

            if (newObject && quickSnap) {
                float distance = Vector3.Distance(targetPosition, rectTransform.position);
                float scaled = Mathf.Min(distance, maxMovementDistance);
                rectTransform.position = targetPosition + ((rectTransform.position - targetPosition).normalized * scaled);
                rectTransform.sizeDelta = Vector2.Lerp(targetSize, rectTransform.sizeDelta, scaled / distance);
            }

            rectTransform.position = Vector3.SmoothDamp(rectTransform.position, targetPosition, ref positionVelocity, quickSnap ? 0.05f : 0.025f);
            rectTransform.sizeDelta = Vector2.SmoothDamp(rectTransform.sizeDelta, targetSize, ref sizeVelocity, quickSnap ? 0.05f : 0.025f);

            if (float.IsNaN(rectTransform.sizeDelta.x) || float.IsNaN(rectTransform.sizeDelta.y)) {
                rectTransform.sizeDelta = Vector2.zero;
                sizeVelocity = default;
            }

            if (fadeValue >= 0) {
                fadeValue = Mathf.Max(0, fadeValue - Time.deltaTime);
                image.color = Color.Lerp(minOpacity, Color.white, fadeValue);
            }
        }
    }
}
