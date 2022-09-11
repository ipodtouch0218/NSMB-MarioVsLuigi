using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// https://answers.unity.com/questions/1564463/how-do-i-have-clickable-buttons-in-a-scroll-rect.html
public class ScrollRectRedirection : MonoBehaviour {

    private GameObject rect;
    private bool passingEvent = false;

    public void Start() {
        rect = GetComponentsInParent<ScrollRect>()[^1].gameObject;

        EventTrigger trigger = GetComponent<EventTrigger>();

        EventTrigger.Entry entry = new();
        entry.eventID = EventTriggerType.BeginDrag;
        entry.callback.AddListener((data) => { OnBeginDrag((PointerEventData) data); });
        trigger.triggers.Add(entry);

        entry = new();
        entry.eventID = EventTriggerType.Drag;
        entry.callback.AddListener((data) => { OnDrag((PointerEventData) data); });
        trigger.triggers.Add(entry);

        entry = new();
        entry.eventID = EventTriggerType.EndDrag;
        entry.callback.AddListener((data) => { OnEndDrag((PointerEventData) data); });
        trigger.triggers.Add(entry);

        entry = new();
        entry.eventID = EventTriggerType.Scroll;
        entry.callback.AddListener((data) => { OnScrollWheel((PointerEventData) data); });
        trigger.triggers.Add(entry);
    }

    public void OnBeginDrag(PointerEventData pointerEventData) {
        // If you only need to pass the drag through use
        ExecuteEvents.Execute(rect, pointerEventData, ExecuteEvents.beginDragHandler);
        passingEvent = true;
    }

    public void OnDrag(PointerEventData pointerEventData) {
        if (passingEvent)
            ExecuteEvents.Execute(rect, pointerEventData, ExecuteEvents.dragHandler);
    }

    public void OnEndDrag(PointerEventData pointerEventData) {
        ExecuteEvents.Execute(rect, pointerEventData, ExecuteEvents.endDragHandler);
        passingEvent = false;
    }

    public void OnScrollWheel(PointerEventData pointerEventData) {
        ExecuteEvents.Execute(rect, pointerEventData, ExecuteEvents.scrollHandler);
        passingEvent = false;
    }
}
