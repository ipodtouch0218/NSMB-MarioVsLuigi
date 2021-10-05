using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Realtime;

public class RoomIcon : MonoBehaviour {
    Image icon;
    public RoomInfo room;
    public Color defaultColor, highlightColor, selectedColor;
    void Start() {
        icon = GetComponent<Image>();
        Unselect();
    }
    public void Select() {
        icon.color = selectedColor;
    }
    public void Unselect() {
        icon.color = defaultColor;
    }
    public void Hover() {
        icon.color = highlightColor;
    }
    public void Unhover() {
        if (MainMenuManager.Instance.selectedRoom == this) {
            Select();
        } else {
            Unselect();
        }
    }
}
