using NSMB.Quantum;
using NSMB.Utilities.Extensions;
using Quantum;
using UnityEngine;

namespace NSMB.Cameras {
    public class SecondaryCameraPositioner : QuantumSceneViewComponent<StageContext> {

        //---Serialized Variables
        [SerializeField] private Camera mainCamera;
        [SerializeField] private Camera ourCamera;
        [SerializeField] private UnityEngine.LayerMask alwaysIgnoreMask;
        [SerializeField] private bool copyPropertiesOnly;

        //---Private Variables
        private bool destroyed;

        public void OnValidate() {
            this.SetIfNull(ref ourCamera);
        }

        public void UpdatePosition() {
            if (!copyPropertiesOnly) {
                if (destroyed) {
                    return;
                }

                VersusStageData stage = ViewContext.Stage;

                if (!stage.IsWrappingLevel) {
                    Destroy(gameObject);
                    destroyed = true;
                    return;
                }

                float camX = mainCamera.transform.position.x;
                bool enable = Mathf.Abs(camX - stage.StageWorldMin.X.AsFloat) < (mainCamera.orthographicSize * mainCamera.aspect) || Mathf.Abs(camX - stage.StageWorldMax.X.AsFloat) < (mainCamera.orthographicSize * mainCamera.aspect);

                ourCamera.enabled = enable;

                if (enable) {
                    float middle = stage.StageWorldMin.X.AsFloat + stage.TileDimensions.X * 0.25f;
                    bool rightHalf = mainCamera.transform.position.x > middle;
                    transform.localPosition = new(stage.TileDimensions.X * (rightHalf ? -1 : 1) * 0.5f, 0, 0);
                }
            }

            ourCamera.orthographicSize = mainCamera.orthographicSize;
            ourCamera.cullingMask = mainCamera.cullingMask & ~alwaysIgnoreMask;
        }
    }
}
