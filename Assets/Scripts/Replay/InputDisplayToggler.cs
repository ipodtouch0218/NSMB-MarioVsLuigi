using UnityEngine;

public class InputDisplayToggler : MonoBehaviour {

    public void Start() {
        Settings.OnInputDisplayActiveChanged += OnInputDisplayActiveChanged;
        OnInputDisplayActiveChanged(Settings.Instance.GraphicsInputDisplay);
    }

    public void OnDestroy() {
        Settings.OnInputDisplayActiveChanged -= OnInputDisplayActiveChanged;
    }

    private void OnInputDisplayActiveChanged(bool active) {
        gameObject.SetActive(active);
    }
}
