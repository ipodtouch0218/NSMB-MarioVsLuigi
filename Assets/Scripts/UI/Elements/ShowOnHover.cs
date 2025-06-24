using NSMB.Utilities.Extensions;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NSMB.UI.Elements {
    public class ShowOnHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {

        //---Serialized Variables
        [SerializeField] private Canvas parentCanvas;
        [SerializeField] private GameObject objectToShow;
        [SerializeField] private bool followCursor = true;
        [SerializeField] private Vector2 offset;

        public void OnValidate() {
            this.SetIfNull(ref parentCanvas, UnityExtensions.GetComponentType.Parent);
        }

        public void OnEnable() {
            if (objectToShow) {
                objectToShow.SetActive(false);
            }
        }

        public void OnDisable() {
            if (objectToShow) {
                objectToShow.SetActive(false);
            }
        }

        public void Update() {
            if (objectToShow && objectToShow.activeInHierarchy && followCursor) {
                objectToShow.transform.position = Settings.Controls.UI.Point.ReadValue<Vector2>() + offset;
            }
        }

        public void OnPointerEnter(PointerEventData eventData) {
            if (objectToShow) {
                objectToShow.SetActive(true);
            }
        }

        public void OnPointerExit(PointerEventData eventData) {
            if (objectToShow) {
                objectToShow.SetActive(false);
            }
        }
    }
}
