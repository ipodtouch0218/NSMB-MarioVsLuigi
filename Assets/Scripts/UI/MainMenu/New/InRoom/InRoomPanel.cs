using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NSMB.UI.MainMenu.Submenus {
    public class InRoomPanel : MonoBehaviour {

        //---Properties
        public virtual GameObject DefaultSelectedObject => defaultSelectedObject;

        //---Serialized Variables
        [SerializeField] protected InRoomSubmenu menu;
        [SerializeField] public InRoomPanel leftPanel, rightPanel;
        [SerializeField] private List<GameObject> hideWhenNotSelected;
        [SerializeField] private GameObject defaultSelectedObject;
        [SerializeField] private TMP_Text header;
        [SerializeField] private Color selectedColor, deselectedColor;

        public virtual void Select(bool setDefault) {
            foreach (var hide in hideWhenNotSelected) {
                hide.SetActive(true);
            }
            header.color = selectedColor;

            if (setDefault) {
                EventSystem.current.SetSelectedGameObject(DefaultSelectedObject);
            }
        }

        public virtual void Deselect() {
            foreach (var hide in hideWhenNotSelected) {
                hide.SetActive(false);
            }
            header.color = deselectedColor;
        }
    }
}
