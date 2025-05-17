using NSMB.UI.MainMenu.Submenus.Prompts;
using Quantum;
using UnityEngine;
using UnityEngine.EventSystems;

public class ChangeDescription : MonoBehaviour, ISelectHandler {

    //---Serialized Variables
    [SerializeField] private GameSettingsPromptSubmenu submenu;
    [SerializeField] private CommandChangeRules.Rules rule;

    public void OnSelect(BaseEventData eventData) {
        submenu.UpdateDescription(rule);
    }
}
