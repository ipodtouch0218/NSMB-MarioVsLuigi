using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class Clickable : MonoBehaviour, /*IPointerClickHandler,*/ IPointerDownHandler {

    public UnityEvent OnClick;
/*
    public void OnPointerClick(PointerEventData eventData) {
        OnClick?.Invoke();
    }
*/
    public void OnPointerDown(PointerEventData eventData) {
        OnClick?.Invoke();
    }
}
