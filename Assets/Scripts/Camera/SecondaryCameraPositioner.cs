using UnityEngine;

public class SecondaryCameraPositioner : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private Transform mainCameraTransform;
    [SerializeField] private Camera secondaryCamera;

    //---Private Variables
    private bool destroyed;

    public void OnValidate() {
        if (!secondaryCamera) secondaryCamera = GetComponent<Camera>();
        if (mainCameraTransform == null) mainCameraTransform = transform.parent;
    }

    public void UpdatePosition() {
        if (!GameManager.Instance || destroyed)
            return;

        GameManager gm = GameManager.Instance;

        if (!gm.loopingLevel) {
            Destroy(gameObject);
            destroyed = true;
            return;
        }

        bool enable =
            mainCameraTransform.localPosition.x > gm.LevelMinX - 1 && mainCameraTransform.localPosition.x < gm.LevelMinX + 7
            || mainCameraTransform.localPosition.x < gm.LevelMaxX + 1 && mainCameraTransform.localPosition.x > gm.LevelMaxX - 7;

        secondaryCamera.enabled = enable;

        if (enable) {
            bool rightHalf = mainCameraTransform.localPosition.x > gm.LevelMiddleX;
            transform.localPosition = new(gm.levelWidthTile * (rightHalf ? -1 : 1), 0, 0);
        }
    }
}
