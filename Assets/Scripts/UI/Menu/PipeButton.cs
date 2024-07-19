using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace NSMB.UI.MainMenu {
    public class PipeButton : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private RectTransform rect;
        [SerializeField] private Button button;
        [SerializeField] private Image image;

        [SerializeField] private Color selectedColor = Color.white;
        [SerializeField] private Color deselectedColor = Color.gray;
        [SerializeField] private bool leftAnchored;

        //---Private Variables
        private Color disabledColor;
        private Vector2 anchor, adjustedAnchor;

        public void OnValidate() {
            if (!rect) {
                rect = GetComponent<RectTransform>();
            }

            if (!button) {
                button = GetComponent<Button>();
            }

            if (!image) {
                image = GetComponentInChildren<Image>();
            }
        }

        public void Start() {
            anchor = leftAnchored ? rect.anchorMax : rect.anchorMin;
            adjustedAnchor = anchor + Vector2.right * (leftAnchored ? -0.1f : 0.1f);
            disabledColor = new(deselectedColor.r, deselectedColor.g, deselectedColor.b, deselectedColor.a * 0.5f);
        }

        public void Update() {
            if (!button.IsInteractable()) {
                SetAnchor(adjustedAnchor);
                image.color = disabledColor;
                return;
            }
            if (EventSystem.current.currentSelectedGameObject == gameObject) {
                SetAnchor(anchor);
                image.color = selectedColor;
            } else {
                SetAnchor(adjustedAnchor);
                image.color = deselectedColor;
            }
        }

        private void SetAnchor(Vector2 value) {
            if (leftAnchored) {
                rect.anchorMax = value;
            } else {
                rect.anchorMin = value;
            }
        }
    }
}
