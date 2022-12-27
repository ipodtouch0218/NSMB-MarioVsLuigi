using UnityEngine;

public class SecondaryCameraPositioner : MonoBehaviour {

    private new Camera camera;
    private bool destroyed = false;

    public void Start() {
        camera = Camera.main;
    }

    public void UpdatePosition() {
        if (!GameManager.Instance || destroyed)
            return;

        if (!GameManager.Instance.loopingLevel) {
            Destroy(gameObject);
            destroyed = true;
            return;
        }

        bool right = camera.transform.position.x > GameManager.Instance.LevelMiddleX;
        transform.localPosition = new Vector3(GameManager.Instance.levelWidthTile * (right ? -1 : 1), 0, 0);
    }
}
