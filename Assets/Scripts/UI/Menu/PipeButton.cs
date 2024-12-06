using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using NSMB.Extensions;

namespace NSMB.UI.MainMenu {
    public class PipeButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {

        //---Serialized Variables
        [SerializeField] private RectTransform rect;
        [SerializeField] private Button button;
        [SerializeField] private Image image;

        [SerializeField] private Color selectedColor = Color.white, deselectedColor = Color.gray;
        [SerializeField] private Vector2 sizeDecreasePixels = new Vector2(50f, 0);

        //---Private Variables
        private Color disabledColor;
        private Vector2 size;
        private bool hover;

        public void OnValidate() {
            this.SetIfNull(ref rect);
            this.SetIfNull(ref button);
            this.SetIfNull(ref image, UnityExtensions.GetComponentType.Children);
        }

        public void Start() {
            size = rect.sizeDelta;
            disabledColor = new(deselectedColor.r, deselectedColor.g, deselectedColor.b, deselectedColor.a * 0.5f);
        }

        public void Update() {
            if (button && !button.IsInteractable()) {
                rect.sizeDelta = size - sizeDecreasePixels;
                image.color = disabledColor;
                return;
            }
            if (hover || EventSystem.current.currentSelectedGameObject == gameObject) {
                rect.sizeDelta = size;
                image.color = selectedColor;
            } else {
                rect.sizeDelta = size - sizeDecreasePixels;
                image.color = deselectedColor;
            }
        }

        public void OnPointerEnter(PointerEventData eventData) {
            hover = true;
        }

        public void OnPointerExit(PointerEventData eventData) {
            hover = false;
        }

    }
}
