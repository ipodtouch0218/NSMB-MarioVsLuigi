using NSMB.UI.Elements;
using NSMB.Utilities.Extensions;
using UnityEngine;

namespace NSMB.UI.Game {
    public class HUDScaler : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private ScaleWithParent scaler;
        [SerializeField] private float baseline = 1000f;
        [SerializeField] private float pxPerStep = 150;

        public void OnValidate() {
            this.SetIfNull(ref scaler);
        }

        public void OnEnable() {
            Settings.OnHudScaleChanged += OnHudScaleChanged;
            OnHudScaleChanged(Settings.Instance.GraphicsHudScale);
        }

        public void OnDisable() {
            Settings.OnHudScaleChanged -= OnHudScaleChanged;
            scaler.targetWidth = baseline;
        }

        private void OnHudScaleChanged(float value) {
            scaler.targetWidth = baseline + (pxPerStep * (8 - value));
        }
    }
}