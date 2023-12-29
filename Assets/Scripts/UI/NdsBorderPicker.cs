using UnityEngine;
using UnityEngine.UI;

public class NdsBorderPicker : MonoBehaviour {

    //---Static Variables
    private static readonly Color Transparent = new(0, 0, 0, 0);

    //---Serialized Variables
    [SerializeField] private Sprite[] borders;
    [SerializeField] private Image image;

    public void OnValidate() {
        image = GetComponent<Image>();
    }

    public void OnEnable() {
        Settings.OnNdsBorderChanged += ChangeBorder;
        ChangeBorder();
    }

    public void OnDisable() {
        Settings.OnNdsBorderChanged -= ChangeBorder;
    }

    private void ChangeBorder() {
        image.sprite = borders[Settings.Instance.GraphicsNdsBorder];
        image.color = image.sprite ? Color.white : Transparent;
    }
}
