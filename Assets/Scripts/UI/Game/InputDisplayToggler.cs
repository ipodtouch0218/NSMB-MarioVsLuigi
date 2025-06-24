using UnityEngine;

namespace NSMB.UI.Game {
    public class InputDisplayToggler : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private GameObject child;

        public void Start() {
            Settings.OnInputDisplayActiveChanged += OnInputDisplayActiveChanged;
            OnInputDisplayActiveChanged(Settings.Instance.GraphicsInputDisplay);
        }

        public void OnDestroy() {
            Settings.OnInputDisplayActiveChanged -= OnInputDisplayActiveChanged;
        }

        private void OnInputDisplayActiveChanged(bool active) {
            child.SetActive(active);
        }
    }
}
