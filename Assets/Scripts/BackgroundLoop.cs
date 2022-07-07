using System;
using UnityEngine;

public class BackgroundLoop : MonoBehaviour {

    public static BackgroundLoop instance = null;

    private GameObject[] children;
    private Vector3[] truePositions, positionsAfterPixelSnap;
    private float[] ppus;
    private float[] halfWidths;

    private Camera mainCamera;
    private Vector2 screenBounds;
    private Vector3 lastPosition;

    public bool wrap;

    public void Start() {
        instance = this;
        Transform t = GameObject.FindGameObjectWithTag("Backgrounds").transform;
        children = new GameObject[t.childCount];
        ppus = new float[t.childCount];
        truePositions = new Vector3[t.childCount];
        positionsAfterPixelSnap = new Vector3[t.childCount];
        halfWidths = new float[t.childCount];
        for (int i = 0; i < t.childCount; i++) {
            children[i] = t.GetChild(i).gameObject;
            SpriteRenderer sr = children[i].GetComponent<SpriteRenderer>();
            ppus[i] = sr.sprite.pixelsPerUnit;
            halfWidths[i] = sr.bounds.extents.x - (0.30f/ppus[i]);
            positionsAfterPixelSnap[i] = truePositions[i] = children[i].transform.position;
        }

        mainCamera = gameObject.GetComponent<Camera>();
        screenBounds = new Vector2(mainCamera.orthographicSize * mainCamera.aspect, mainCamera.orthographicSize) * 2.5f; // mainCamera.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, mainCamera.transform.position.z));
        foreach (GameObject obj in children)
            LoadChildObjects(obj);

        lastPosition = transform.position;
    }

    public void LateUpdate() {
        Reposition();
    }
    void LoadChildObjects(GameObject obj) {
        float objectWidth = obj.GetComponent<SpriteRenderer>().bounds.size.x;
        int childsNeeded = (int) Mathf.Ceil(screenBounds.x / objectWidth) + 1;
        GameObject clone = Instantiate(obj);
        for (int i = 0; i <= childsNeeded; i++) {
            GameObject c = Instantiate(clone);
            c.transform.SetParent(obj.transform);
            c.transform.position = new Vector3(objectWidth * i, obj.transform.position.y, obj.transform.position.z);
            c.name = obj.name + i;
        }
        Destroy(clone);
        Destroy(obj.GetComponent<SpriteRenderer>());
    }
    void RepositionChildObjects(GameObject obj) {
        if (!obj)
            return;

        Transform parent = obj.transform;
        if (parent.childCount > 1) {
            GameObject firstChild = parent.GetChild(0).gameObject;
            GameObject lastChild = parent.GetChild(parent.childCount - 1).gameObject;
            float halfObjectWidth = halfWidths[Array.IndexOf(children, obj)];
            if (transform.position.x + screenBounds.x > lastChild.transform.position.x + halfObjectWidth) {
                firstChild.transform.SetAsLastSibling();
                firstChild.transform.position = new Vector3(lastChild.transform.position.x + halfObjectWidth * 2, lastChild.transform.position.y, lastChild.transform.position.z);
            } else if (transform.position.x - screenBounds.x < firstChild.transform.position.x - halfObjectWidth) {
                lastChild.transform.SetAsFirstSibling();
                lastChild.transform.position = new Vector3(firstChild.transform.position.x - halfObjectWidth * 2, firstChild.transform.position.y, firstChild.transform.position.z);
            }
        }
    }
    public void Reposition() {

        if (wrap) {
            for (int i = 0; i < children.Length; i++) {
                GameObject obj = children[i];
                float difference = (transform.position.x - lastPosition.x) + (obj.transform.position.x - truePositions[i].x);

                truePositions[i] += difference * Vector3.right;
                obj.transform.position = positionsAfterPixelSnap[i] = PixelClamp(truePositions[i], obj.transform.lossyScale, ppus[i]);

                RepositionChildObjects(obj);
            }
            wrap = false;
        } else {
            for (int i = 0; i < children.Length; i++) {
                GameObject obj = children[i];
                float difference = (transform.position.x - lastPosition.x) + (obj.transform.position.x - positionsAfterPixelSnap[i].x);
                float parallaxSpeed = 1 - Mathf.Clamp01(Mathf.Abs(lastPosition.z / obj.transform.position.z));

                truePositions[i] += difference * parallaxSpeed * Vector3.right;
                obj.transform.position = positionsAfterPixelSnap[i] = PixelClamp(truePositions[i], obj.transform.lossyScale, ppus[i]);

                RepositionChildObjects(obj);
            }
        }
        lastPosition = transform.position;
    }

    private static Vector3 PixelClamp(Vector3 pos, Vector3 scale, float pixelsPerUnit) {

        if (!Settings.Instance.ndsResolution)
            return pos;

        pos *= pixelsPerUnit;
        pos = pos.Divide(scale);

        pos.x = Mathf.CeilToInt(pos.x);
        pos.y = Mathf.CeilToInt(pos.y);
        pos.z = Mathf.CeilToInt(pos.z);

        pos /= pixelsPerUnit;
        pos = pos.Multiply(scale);

        return pos;
    }
}