using NSMB.Extensions;
using NSMB.UI.MainMenu;
using NSMB.Utils;
using UnityEngine;
using UnityEngine.UI;

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
        UpdateColor();
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