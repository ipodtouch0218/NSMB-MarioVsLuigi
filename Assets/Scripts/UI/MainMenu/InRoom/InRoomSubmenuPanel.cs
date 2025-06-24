using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace NSMB.UI.MainMenu.Submenus.InRoom {
    public class InRoomSubmenuPanel : MonoBehaviour {

        //---Properties
        public virtual GameObject DefaultSelectedObject => defaultSelectedObject;
        public virtual bool IsInSubmenu => false;

        //---Serialized Variables
        [SerializeField] protected InRoomSubmenu menu;
        [SerializeField] public InRoomSubmenuPanel leftPanel, rightPanel;
        [SerializeField] private List<GameObject> hideWhenNotSelected;
        [SerializeField] private GameObject defaultSelectedObject;
        [SerializeField] private TMP_Text header;
        [SerializeField] private Color selectedColor, deselectedColor;

        public virtual void Initialize() { }

        public virtual void OnDestroy() { } 

        public virtual void Select(bool setDefault) {
            foreach (var hide in hideWhenNotSelected) {
                hide.SetActive(true);
            }
            header.color = selectedColor;

            if (setDefault) {
                menu.Canvas.EventSystem.SetSelectedGameObject(DefaultSelectedObject);
            }
        }

        public virtual void Deselect() {
            foreach (var hide in hideWhenNotSelected) {
                hide.SetActive(false);
            }
            header.color = deselectedColor;
        }

        public virtual bool TryGoBack(out bool playSound) {
            playSound = true;
            return true;
        }
    }
}
