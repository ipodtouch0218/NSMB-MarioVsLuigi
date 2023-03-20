using UnityEngine;
using TMPro;

using NSMB.UI.Pause.Loaders;

namespace NSMB.UI.Pause.Options {
    public class PauseOption : MonoBehaviour {

        //---Public Variables
        public RectTransform rectTransform;

        //---Serialized Variables
        [SerializeField] private PauseOptionMenuManager manager;
        [SerializeField] internal TMP_Text label;
        [SerializeField] protected PauseOptionLoader loader;

        //---Private Variables
        protected string originalText;

        public virtual void OnValidate() {
            if (!manager) manager = GetComponentInParent<PauseOptionMenuManager>();
            if (!loader) loader = GetComponent<PauseOptionLoader>();
        }

        public void Awake() {
            rectTransform = GetComponent<RectTransform>();
        }

        public virtual void OnEnable() {
            if (loader)
                loader.LoadOptions(this);
        }

        public virtual void Selected() {
            originalText ??= label.text;
            label.text = "» " + originalText;
        }

        public virtual void Deselected() {
            originalText ??= label.text;
            label.text = originalText;
        }

        public virtual void OnClick() { }
        public virtual void OnLeftPress() { }
        public virtual void OnLeftHeld() { }
        public virtual void OnRightPress() { }
        public virtual void OnRightHeld() { }

    }
}
