using NSMB.Extensions;
using UnityEngine;

public class ResizingCamera : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] protected Camera ourCamera;

    public virtual void OnValidate() {
        this.SetIfNull(ref ourCamera);
    }

    public virtual void Start() {
        ClampCameraAspectRatio();
    }

    protected void ClampCameraAspectRatio(float target = 14f/4f) {
        float aspect = ourCamera.aspect;

        // https://forum.unity.com/threads/how-to-calculate-horizontal-field-of-view.16114/#post-2961964
        double aspectReciprocals = 1d / aspect;
        ourCamera.orthographicSize = Mathf.Min(target, (float) (target * (16d/9d) * aspectReciprocals));
    }
}