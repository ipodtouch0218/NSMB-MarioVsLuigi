using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class KeepChildInFocus : MonoBehaviour {
    public RectTransform contentPanel;
    private ScrollRect us;

    void Start() {
        us = GetComponent<ScrollRect>();
    }

    public void Update() {
        if (!EventSystem.current.currentSelectedGameObject)
            return;

        RectTransform target = EventSystem.current.currentSelectedGameObject.GetComponent<RectTransform>();
        if (target.IsChildOf(transform) && target.name != "Scrollbar Vertical")
            UIExtensions.ScrollToCenter(us, target);
    }
}