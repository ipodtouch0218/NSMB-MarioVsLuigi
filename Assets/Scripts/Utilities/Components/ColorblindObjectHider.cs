using NSMB;
using UnityEngine;

public class ColorblindObjectHider : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private GameObject[] objectsToHide;
    [SerializeField] private bool hideWhenColorblindModeEnabled;

    public void OnEnable() {
        Settings.OnColorblindModeChanged += OnColorblindModeChanged;
        OnColorblindModeChanged();
    }

    public void OnDisable() {
        Settings.OnColorblindModeChanged -= OnColorblindModeChanged;
    }

    private void OnColorblindModeChanged() {
        foreach (var obj in objectsToHide) {
            obj.SetActive(Settings.Instance.GraphicsColorblind ^ hideWhenColorblindModeEnabled);
        }
    }
}