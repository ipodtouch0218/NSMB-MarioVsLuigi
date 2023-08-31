using UnityEngine;
using UnityEngine.UI;

public class UseNdsRenderTexture : MonoBehaviour {

    [SerializeField] private RawImage image;

    public void OnValidate() {
        image = GetComponent<RawImage>();
    }

    public void Awake() {
        GlobalController.Instance.RenderTextureChanged += OnTextureChanged;
        OnTextureChanged(GlobalController.Instance.ndsTexture);
    }

    private void OnTextureChanged(RenderTexture texture) {
        image.texture = texture;
    }
}
