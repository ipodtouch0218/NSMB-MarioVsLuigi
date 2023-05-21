using UnityEngine;

using NSMB.Game;

public class SecondaryCameraPositioner : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Camera secondaryCamera;

    //---Private Variables
    private bool destroyed;

    public void OnValidate() {
        if (!secondaryCamera) secondaryCamera = GetComponent<Camera>();
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
            mainCamera.transform.position.x > gm.LevelMinX - 1 && mainCamera.transform.position.x < gm.LevelMinX + 7
            || mainCamera.transform.position.x < gm.LevelMaxX + 1 && mainCamera.transform.position.x > gm.LevelMaxX - 7;

        secondaryCamera.enabled = enable;

        if (enable) {
            bool rightHalf = mainCamera.transform.position.x > gm.LevelMiddleX;
            transform.localPosition = new(gm.levelWidthTile * (rightHalf ? -1 : 1), 0, 0);
        }
    }
}
