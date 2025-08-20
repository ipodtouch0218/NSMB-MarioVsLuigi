using NSMB.Utilities.Extensions;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace NSMB.UI {
    [RequireComponent(typeof(InputSystemUIInputModule))]
    public class EventSystemRebindableControls : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private InputSystemUIInputModule inputSystem;

        public void OnValidate() {
            this.SetIfNull(ref inputSystem);
        }

        public void OnEnable() {
            inputSystem.actionsAsset = Settings.Controls.asset;
            inputSystem.point = InputActionReference.Create(Settings.Controls.UI.Point);
            inputSystem.leftClick = InputActionReference.Create(Settings.Controls.UI.Click);
            inputSystem.middleClick = InputActionReference.Create(Settings.Controls.UI.MiddleClick);
            inputSystem.rightClick = InputActionReference.Create(Settings.Controls.UI.RightClick);
            inputSystem.scrollWheel = InputActionReference.Create(Settings.Controls.UI.ScrollWheel);
            inputSystem.move = InputActionReference.Create(Settings.Controls.UI.Navigate);
            inputSystem.submit = InputActionReference.Create(Settings.Controls.UI.Submit);
            inputSystem.cancel = InputActionReference.Create(Settings.Controls.UI.Cancel);
            Settings.Controls.asset.Enable();
        }

        public void OnDisable() {
            // For some reason, InputSystemUIInputModule breaks everything OnDisable. Thanks for that.
            inputSystem.actionsAsset = null;
            Settings.Controls.asset.Enable();
        }
    }
}
