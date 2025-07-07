using Quantum;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NSMB.UI.MainMenu.Submenus.Prompts {
    public class ChangeDescription : MonoBehaviour, ISelectHandler {

        //---Serialized Variables
        [SerializeField] private RulesGameSettingsPanel panel;
        [SerializeField] private CommandChangeRules.Rules rule;

        public void OnSelect(BaseEventData eventData) {
            panel.UpdateDescription(rule);
        }
    }
}
