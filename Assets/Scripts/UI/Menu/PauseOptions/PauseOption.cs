using UnityEngine;
using TMPro;

using NSMB.UI.Pause.Loaders;
using UnityEngine.Events;

namespace NSMB.UI.Pause.Options {
    public class PauseOption : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private PauseOptionMenuManager manager;
        [SerializeField] private TMP_Text label;
        [SerializeField] protected PauseOptionLoader loader;

        //---Private Variables
        private string originalText;

        public virtual void OnValidate() {
            if (!manager) manager = GetComponentInParent<PauseOptionMenuManager>();
            if (!loader) loader = GetComponent<PauseOptionLoader>();
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
        public virtual void OnRightPress() { }

    }
}
