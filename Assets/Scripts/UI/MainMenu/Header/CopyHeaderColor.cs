using NSMB.Utilities;
using NSMB.Utilities.Extensions;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu.Header {
    [RequireComponent(typeof(Image))]
    public class CopyHeaderColor : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private MainMenuCanvas canvas;
        [SerializeField] private Color multiplyColor = Color.white;
        [SerializeField] private Image target;

        public void OnValidate() {
            this.SetIfNull(ref canvas, UnityExtensions.GetComponentType.Parent);
            this.SetIfNull(ref target);
        }

        public void OnEnable() {
            canvas.HeaderColorChanged += OnHeaderColorChanged;
            UpdateColor();
        }

        public void OnDisable() {
            canvas.HeaderColorChanged -= OnHeaderColorChanged;
        }

        private void UpdateColor() {
            Color newColor = canvas.HeaderColor * multiplyColor;
            newColor.a = 0.5f * (1f - Utils.Luminance(newColor));
            target.color = newColor;
        }

        private void OnHeaderColorChanged(Color color) {
            UpdateColor();
        }
    }
}
