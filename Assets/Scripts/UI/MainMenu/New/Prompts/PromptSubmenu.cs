using UnityEngine;
using UnityEngine.EventSystems;

namespace NSMB.UI.MainMenu.Submenus.Prompts {
    public class PromptSubmenu : MainMenuSubmenu {

        //---Serialized Variables
        [SerializeField] private GameObject backButton;

        public override bool TryGoBack(out bool playSound) {
            if (backButton && EventSystem.current.currentSelectedGameObject != backButton) {
                EventSystem.current.SetSelectedGameObject(backButton);
                playSound = false;
                return false;
            }

            return base.TryGoBack(out playSound);
        }
    }
}