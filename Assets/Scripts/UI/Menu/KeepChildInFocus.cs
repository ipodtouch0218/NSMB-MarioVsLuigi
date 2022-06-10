using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(ScrollRect))]
public class KeepChildInFocus : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
    public float scrollAmount = 15;
    private bool mouseOver = false;
    private ScrollRect rect;
    private float scrollPos = 0;

    void Awake() {
        rect = GetComponent<ScrollRect>();
    }
    void Update() {
        if (mouseOver || rect.content == null)
            return;
        
        rect.verticalNormalizedPosition = Mathf.Lerp(rect.verticalNormalizedPosition, scrollPos, scrollAmount * Time.deltaTime);

        if (!EventSystem.current.currentSelectedGameObject)
            return;

        RectTransform target = EventSystem.current.currentSelectedGameObject.GetComponent<RectTransform>();

        if (IsFirstParent(target) && target.name != "Scrollbar Vertical") {
            scrollPos = Extensions.ScrollToCenter(rect, target, false);
        } else {
            scrollPos = rect.verticalNormalizedPosition;
        }
    }

    private readonly List<ScrollRect> components = new();
    private bool IsFirstParent(Transform target) {
        do {
            if (target.GetComponent<IFocusIgnore>() != null)
                return false;

            target.GetComponents(components);

            if (components.Count >= 1)
                return components.Contains(rect);

            target = target.parent;
        } while (target != null);

        return false;
    }

    public void OnPointerEnter(PointerEventData eventData) {
        mouseOver = true;
    }
    public void OnPointerExit(PointerEventData eventData) {
        mouseOver = false;
    }

    public interface IFocusIgnore { }
}