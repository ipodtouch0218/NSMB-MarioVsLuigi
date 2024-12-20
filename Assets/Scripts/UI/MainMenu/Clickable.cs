using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class Clickable : MonoBehaviour, IPointerDownHandler, IPointerExitHandler, IPointerUpHandler {

    public UnityEvent OnClick;
    public bool Interactable = true, RepeatOnHold = false;

    //---Private Variables
    private bool held;
    private float holdTime;

    public void Update() {
        if (held) {
            if (!Interactable || !RepeatOnHold) {
                held = false;
                return;
            }

            if ((holdTime += Time.unscaledDeltaTime) > 0.5f) {
                OnClick?.Invoke();
                holdTime = 0.4f;
            }
        }
    }

    public void OnPointerDown(PointerEventData eventData) {
        if (Interactable) {
            OnClick?.Invoke();
            held = true;
            holdTime = 0; 
        }
    }

    public void OnPointerExit(PointerEventData eventData) {
        held = false;
    }

    public void OnPointerUp(PointerEventData eventData) {
        held = false;
    }
}
