using NSMB.Utilities.Extensions;
using UnityEngine;

namespace NSMB.Cameras {
    public class ResizingCamera : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] protected Camera ourCamera;

        public virtual void OnValidate() {
            this.SetIfNull(ref ourCamera);
        }

        public virtual void Start() {
            ClampCameraAspectRatio();
        }

        public virtual void Update() {
            ClampCameraAspectRatio();
        }

        protected void ClampCameraAspectRatio(float target = 14f/4f) {
            float aspect = ourCamera.aspect;
            if (Mathf.Abs((16f / 9f) - aspect) < 0.05f) {
                aspect = 16f / 9f;
            }

            // https://forum.unity.com/threads/how-to-calculate-horizontal-field-of-view.16114/#post-2961964
            double aspectReciprocals = 1d / aspect;
            ourCamera.orthographicSize = Mathf.Min(target, (float) (target * (16d/9d) * aspectReciprocals));
        }
    }
}
