using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class KeepChildInFocus : MonoBehaviour {
    public RectTransform contentPanel;

    public void Update() {
        if (!EventSystem.current.currentSelectedGameObject)
            return;

        RectTransform target = EventSystem.current.currentSelectedGameObject.GetComponent<RectTransform>();
        if (target.IsChildOf(transform))
            SnapTo(target);
    }

    //https://stackoverflow.com/questions/30766020/how-to-scroll-to-a-specific-element-in-scrollrect-with-unity-ui
    public void SnapTo(RectTransform target) {
        Canvas.ForceUpdateCanvases();

        contentPanel.anchoredPosition =
                (Vector2) transform.InverseTransformPoint(contentPanel.position)
                - (Vector2) transform.InverseTransformPoint(target.position);
    }
}