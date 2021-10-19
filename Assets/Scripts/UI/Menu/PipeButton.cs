using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PipeButton : MonoBehaviour {

    private Button button;
    private RectTransform rect;
    private Vector2 anchor;
    void Start() {
        rect = GetComponent<RectTransform>();
        button = GetComponent<Button>();
        anchor = rect.anchorMin;
        Unselect();
    }

    public void Selected() {
        if (button == null) return;
        if (button.interactable)
            rect.anchorMin = anchor;
    }
    public void Unselect() {
        rect.anchorMin = anchor + new Vector2(0.1f,0);
    }
}
