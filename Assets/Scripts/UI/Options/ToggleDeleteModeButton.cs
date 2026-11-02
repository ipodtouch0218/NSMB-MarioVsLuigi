using UnityEngine;
using UnityEngine.Scripting;

namespace NSMB.UI.Options {
    public class ToggleDeleteModeButton : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private PauseOptionControlsTab tab;

        [Preserve]
        public void Click() {
            tab.DeleteMode = !tab.DeleteMode;
            GlobalController.Instance.PlaySound(SoundEffect.UI_Decide);
        }
    }
}