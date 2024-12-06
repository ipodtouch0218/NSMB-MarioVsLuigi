using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class Clickable : MonoBehaviour, IPointerClickHandler {

    public UnityEvent OnClick;

    public void OnPointerClick(PointerEventData eventData) {
        OnClick?.Invoke();
    }
}
