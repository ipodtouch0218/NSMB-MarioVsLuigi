using UnityEngine;

public class SecondaryCameraPositioner : MonoBehaviour {

    private bool destroyed = false;

    public void UpdatePosition() {
        if (destroyed)
            return;

        if (!GameManager.Instance)
            return;

        if (!GameManager.Instance.loopingLevel) {
            Destroy(gameObject);
            destroyed = true;
            return;
        }

        bool right = Camera.main.transform.position.x > GameManager.Instance.LevelMiddleX;
        transform.localPosition = new Vector3(GameManager.Instance.levelWidthTile * (right ? -1 : 1), 0, 0);
    }
}
