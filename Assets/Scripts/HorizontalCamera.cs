using UnityEngine;
     
[ExecuteInEditMode]
public class HorizontalCamera : MonoBehaviour {
    private new Camera camera;
    private float lastAspect;
    
    public double m_orthographicSize = 5d;
     
    private void OnEnable() {
        RefreshCamera();
    }
     
    private void Update() {
        float aspect = camera.aspect;
        if (aspect != lastAspect)
            AdjustCamera( aspect );
    }
     
    public void RefreshCamera() {
        if (camera == null)
            camera = GetComponent<Camera>();
     
        AdjustCamera(camera.aspect);
    }
     
    private void AdjustCamera(float aspect) {
        lastAspect = aspect;
     
        // Credit: https://forum.unity.com/threads/how-to-calculate-horizontal-field-of-view.16114/#post-2961964
        double _1OverAspect = 1d / aspect;
        camera.orthographicSize = Mathf.Min((float) m_orthographicSize, (float) (m_orthographicSize * (16d/9d) * _1OverAspect));
    }
     
    #if UNITY_EDITOR
        private void OnValidate() {
            RefreshCamera();
        }
    #endif
}
