using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class InRoomPanel : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] public InRoomPanel leftPanel, rightPanel;
    [SerializeField] private List<GameObject> hideWhenNotSelected;
    [SerializeField] private GameObject defaultSelectedObject;
    [SerializeField] private TMP_Text header;
    [SerializeField] private Color selectedColor, deselectedColor;

    public void Select() {
        foreach (var hide in hideWhenNotSelected) {
            hide.SetActive(true);
        }
        header.color = selectedColor;
    }

    public void Deselect() {
        foreach (var hide in hideWhenNotSelected) {
            hide.SetActive(false);
        }
        header.color = deselectedColor;
    }
}
