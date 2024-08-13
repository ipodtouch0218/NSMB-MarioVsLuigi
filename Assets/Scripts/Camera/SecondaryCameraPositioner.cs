using NSMB.Extensions;
using Quantum;
using UnityEngine;

public class SecondaryCameraPositioner : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Camera ourCamera;

    //---Private Variables
    private bool destroyed;
    private VersusStageData stage;

    public void OnValidate() {
        this.SetIfNull(ref ourCamera);
    }

    public void Start() {
        stage = (VersusStageData) QuantumUnityDB.GetGlobalAsset(FindObjectOfType<QuantumMapData>().Asset.UserAsset);
    }

    public void UpdatePosition() {
        if (!stage || destroyed) {
            return;
        }

        if (!stage.IsWrappingLevel) {
            Destroy(gameObject);
            destroyed = true;
            return;
        }

        bool enable =
            mainCamera.transform.position.x > stage.StageWorldMin.X.AsFloat - 1 && mainCamera.transform.position.x < stage.StageWorldMax.X.AsFloat + 7
            || mainCamera.transform.position.x < stage.StageWorldMax.X.AsFloat + 1 && mainCamera.transform.position.x > stage.StageWorldMax.X.AsFloat - 7;

        ourCamera.enabled = enable;
        ourCamera.orthographicSize = mainCamera.orthographicSize;

        if (enable) {
            float middle = stage.StageWorldMin.X.AsFloat + stage.TileDimensions.x * 0.25f;
            bool rightHalf = mainCamera.transform.position.x > middle;
            transform.localPosition = new(stage.TileDimensions.x * (rightHalf ? -1 : 1) * 0.5f, 0, 0);
        }
    }
}