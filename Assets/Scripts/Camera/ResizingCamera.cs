using NSMB.Extensions;
using UnityEngine;

public class ResizingCamera : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] protected Camera ourCamera;

    public virtual void OnValidate() {
        this.SetIfNull(ref ourCamera);
    }

    public virtual void Start() {
        GlobalController.ResolutionChanged += AdjustCamera;
        AdjustCamera();
    }

    public virtual void OnDestroy() {
        GlobalController.ResolutionChanged -= AdjustCamera;
    }

    protected virtual double GetNewCameraSize() {
        return (14f / 4f)/* + SizeIncreaseCurrent*/;
    }

    private void AdjustCamera() {
        float aspect = ourCamera.aspect;
        double size = GetNewCameraSize();

        // https://forum.unity.com/threads/how-to-calculate-horizontal-field-of-view.16114/#post-2961964
        double aspectReciprocals = 1d / aspect;
        ourCamera.orthographicSize = Mathf.Min((float) size, (float) (size * (16d/9d) * aspectReciprocals));
    }
}