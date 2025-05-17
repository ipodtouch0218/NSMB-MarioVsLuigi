using UnityEngine;
using UnityEngine.EventSystems;

namespace NSMB.UI.MainMenu.Submenus.Prompts {
    public class PromptSubmenu : MainMenuSubmenu {

        //---Properites
        public virtual GameObject BackButton => backButton;

        //---Serialized Variables
        [SerializeField] protected GameObject backButton;

        public override bool TryGoBack(out bool playSound) {
            if (BackButton && EventSystem.current.currentSelectedGameObject != BackButton) {
                EventSystem.current.SetSelectedGameObject(BackButton);
                playSound = false;
                return false;
            }

            return base.TryGoBack(out playSound);
        }
    }
}