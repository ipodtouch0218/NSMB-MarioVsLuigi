using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class Clickable : MonoBehaviour, IPointerDownHandler {

    public UnityEvent OnClick;
    public bool Interactable = true;

    public void OnPointerDown(PointerEventData eventData) {
        if (Interactable) {
            OnClick?.Invoke();
        }
    }
}
