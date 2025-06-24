using NSMB.Utilities.Extensions;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace NSMB.UI {
    [RequireComponent(typeof(InputSystemUIInputModule))]
    public class EventSystemRebindableControls : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private InputSystemUIInputModule inputSystem;

        private InputActionReference point, click, middleClick, rightClick, scrollWheel, navigate, submit, cancel;

        public void OnValidate() {
            this.SetIfNull(ref inputSystem);
        }

        public void OnEnable() {
            if (point == null) {
                point = inputSystem.point;
                click = inputSystem.leftClick;
                middleClick = inputSystem.middleClick;
                rightClick = inputSystem.rightClick;
                scrollWheel = inputSystem.scrollWheel;
                navigate = inputSystem.move;
                submit = inputSystem.submit;
                cancel = inputSystem.cancel;
            }

            inputSystem.actionsAsset = Settings.Controls.asset;
            inputSystem.point = point;
            inputSystem.leftClick = click;
            inputSystem.middleClick = middleClick;
            inputSystem.rightClick = rightClick;
            inputSystem.scrollWheel = scrollWheel;
            inputSystem.move = navigate;
            inputSystem.submit = submit;
            inputSystem.cancel = cancel;
        }
    }
}
