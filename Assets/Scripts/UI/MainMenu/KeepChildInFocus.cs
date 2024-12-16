using NSMB.Extensions;
using Quantum;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(ScrollRect))]
public class KeepChildInFocus : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {

    //---Serialized Variables
    [SerializeField] private float scrollAmount = 15;

    //---Private Variables
    private readonly List<ScrollRect> components = new();
    private ScrollRect rect;
    private bool mouseOver = false;
    private float scrollPos = 0;

    public void Awake() {
        this.SetIfNull(ref rect);
    }

    public void Update() {
        if (mouseOver || !rect.content) {
            return;
        }

        rect.verticalNormalizedPosition = Mathf.Lerp(rect.verticalNormalizedPosition, scrollPos, scrollAmount * Time.deltaTime);

        if (!EventSystem.current.currentSelectedGameObject) {
            return;
        }

        RectTransform target = EventSystem.current.currentSelectedGameObject.GetComponent<RectTransform>();

        if (IsFirstParent(target) && target.name != "Scrollbar Vertical") {
            scrollPos = rect.ScrollToCenter(target, false);
        } else {
            scrollPos = rect.verticalNormalizedPosition;
        }
    }

    private bool IsFirstParent(Transform target) {
        do {
            if (target.GetComponent<IFocusIgnore>() != null) {
                return false;
            }

            target.GetComponents(components);

            if (components.Count >= 1) {
                return components.Contains(rect);
            }
        } while (target = target.parent);

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
