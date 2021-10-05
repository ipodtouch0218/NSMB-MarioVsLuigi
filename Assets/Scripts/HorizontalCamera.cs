using UnityEngine;
     
[ExecuteInEditMode]
public class HorizontalCamera : MonoBehaviour {
    private Camera m_camera;
    private float lastAspect;
    
    public double m_orthographicSize = 5d;
     
    private void OnEnable() {
        RefreshCamera();
    }
     
    private void Update() {
        float aspect = m_camera.aspect;
        if (aspect != lastAspect)
            AdjustCamera( aspect );
    }
     
    public void RefreshCamera() {
        if (m_camera == null)
            m_camera = GetComponent<Camera>();
     
        AdjustCamera(m_camera.aspect);
    }
     
    private void AdjustCamera(float aspect) {
        lastAspect = aspect;
     
        // Credit: https://forum.unity.com/threads/how-to-calculate-horizontal-field-of-view.16114/#post-2961964
        double _1OverAspect = 1d / aspect;
        m_camera.orthographicSize = Mathf.Min((float) m_orthographicSize, (float) (m_orthographicSize * (16d/9d) * _1OverAspect));
    }
     
    #if UNITY_EDITOR
        private void OnValidate() {
            RefreshCamera();
        }
    #endif
}
