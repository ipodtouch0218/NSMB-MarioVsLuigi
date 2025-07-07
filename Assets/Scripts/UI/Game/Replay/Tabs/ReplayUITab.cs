using NSMB.Utilities.Extensions;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace NSMB.UI.Game.Replay {
    public class ReplayUITab : MonoBehaviour {

        public virtual GameObject DefaultSelection => defaultSelection;

        //---Serialized Variables
        [SerializeField] protected ReplayUI parent;
        [SerializeField] protected List<GameObject> selectables;
        [SerializeField] public GameObject defaultSelection, selectOnClose;
        [SerializeField] protected Color enabledColor = Color.white, disabledColor = Color.gray;

        public void OnValidate() {
            this.SetIfNull(ref parent, UnityExtensions.GetComponentType.Parent);
        }

        public virtual void OnEnable() {
#if UNITY_EDITOR
            if (!Application.isPlaying) {
                return;
            }
#endif

            Settings.Controls.UI.Cancel.performed += OnCancel;
            EventSystem.current.SetSelectedGameObject(DefaultSelection);
            GlobalController.Instance.sfx.PlayOneShot(SoundEffect.UI_WindowOpen);
        }

        public virtual void OnDisable() {
#if UNITY_EDITOR
            if (!Application.isPlaying) {
                return;
            }
#endif

            Settings.Controls.UI.Cancel.performed -= OnCancel;
            GlobalController.Instance.sfx.PlayOneShot(SoundEffect.UI_WindowClose);
        }

        private void OnCancel(InputAction.CallbackContext context) {
            parent.CloseTab();
        }
    }
}