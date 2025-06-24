using TMPro;
using UnityEngine;

namespace NSMB.UI.MainMenu.Submenus.Prompts {
    public class GameSettingsPanel : MonoBehaviour {

        //---Properties
        public virtual GameObject DefaultSelection => _defaultSelection;
        public virtual GameObject BackButton => _backButton;

        //---Variables
        [SerializeField] protected GameSettingsPromptSubmenu submenu;
        [SerializeField] public TMP_Text header;
        [SerializeField] public GameObject root;
        [SerializeField] private GameObject _backButton;
        [SerializeField] private GameObject _defaultSelection;

        public virtual void OnEnable() {
            submenu.Canvas.EventSystem.SetSelectedGameObject(DefaultSelection);
        }
    }
}
