using UnityEngine;
using UnityEngine.SceneManagement;

public class HorizontalCamera : MonoBehaviour {
    public static float OFFSET_TARGET = 0f;
    private static float OFFSET_VELOCITY, OFFSET = 0f;
    private new Camera camera;
    private float lastAspect;
    
    public bool renderToTextureIfAvailable = true;
    public double orthographicSize = 3.524f;
    public float ndsSize = 3f;

    void Start() {
        RefreshCamera();
    }
     
    private void Update() {
        OFFSET = Mathf.SmoothDamp(OFFSET, OFFSET_TARGET, ref OFFSET_VELOCITY, 1f);
        float aspect = camera.aspect;
        AdjustCamera(aspect);
        camera.targetTexture = (renderToTextureIfAvailable && GlobalController.Instance.ndsMode && SceneManager.GetActiveScene().buildIndex != 0 
            ? GlobalController.Instance.ndsTexture 
            : null);
    }
     
    public void RefreshCamera() {
        if (camera == null)
            camera = GetComponent<Camera>();
     
        AdjustCamera(camera.aspect);
    }
     
    private void AdjustCamera(float aspect) {
        lastAspect = aspect;
        double size = (GlobalController.Instance.ndsMode && SceneManager.GetActiveScene().buildIndex != 0 ? ndsSize : orthographicSize) + OFFSET;
        // double size = orthographicSize;
        // Credit: https://forum.unity.com/threads/how-to-calculate-horizontal-field-of-view.16114/#post-2961964
        double _1OverAspect = 1d / aspect;
        camera.orthographicSize = Mathf.Min((float) size, (float) (size * (16d/9d) * _1OverAspect));
    }
}
