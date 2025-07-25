using NSMB.Utilities.Extensions;
using UnityEngine;

namespace NSMB.UI.Game {
    public class InputDisplayToggler : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private PlayerElements playerElements;
        [SerializeField] private GameObject child;

        public void OnValidate() {
            this.SetIfNull(ref playerElements, UnityExtensions.GetComponentType.Parent);
        }

        public void Start() {
            Settings.OnInputDisplayActiveChanged += OnInputDisplayActiveChanged;
            playerElements.OnCameraFocusChanged += OnCameraFocusChanged;
            OnInputDisplayActiveChanged(Settings.Instance.GraphicsInputDisplay);
        }

        public void OnDestroy() {
            Settings.OnInputDisplayActiveChanged -= OnInputDisplayActiveChanged;
            playerElements.OnCameraFocusChanged -= OnCameraFocusChanged;
        }

        public void UpdateVisibleState() {
            child.SetActive(Settings.Instance.GraphicsInputDisplay && (playerElements.Game == null || playerElements.Game.Frames.Predicted.Exists(playerElements.Entity)));
        }

        private void OnInputDisplayActiveChanged(bool active) {
            UpdateVisibleState();
        }

        private void OnCameraFocusChanged() {
            UpdateVisibleState();
        }
    }
}
