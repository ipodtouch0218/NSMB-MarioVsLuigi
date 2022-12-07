using System;
using UnityEngine;

using NSMB.Extensions;

public class BackgroundLoop : MonoBehaviour {

    //---Static Variables
    public static BackgroundLoop Instance { get; private set; }
    private static readonly Vector2 ScreenBounds = new(7f, 4.5f);

    //---Public Variables
    public bool teleportedThisFrame;

    //---Misc Variables
    private GameObject[] children;
    private Vector3[] truePositions, positionsAfterPixelSnap;
    private float[] ppus, halfWidths;
    private Vector3 lastPosition;

    public void Start() {
        Instance = this;

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
            halfWidths[i] = sr.bounds.extents.x - 0.00004f;
            positionsAfterPixelSnap[i] = truePositions[i] = children[i].transform.position;
        }

        foreach (GameObject obj in children)
            LoadChildObjects(obj);

        lastPosition = transform.position;
    }

    public void LateUpdate() {
        Reposition();
    }

    public void Reposition() {
        for (int i = 0; i < children.Length; i++) {
            GameObject obj = children[i];
            float difference = transform.position.x - lastPosition.x + (obj.transform.position.x - positionsAfterPixelSnap[i].x);
            float parallaxSpeed = 1 - Mathf.Clamp01(Mathf.Abs(lastPosition.z / obj.transform.position.z));

            if (teleportedThisFrame)
                parallaxSpeed = 1;

            truePositions[i] += difference * parallaxSpeed * Vector3.right;
            obj.transform.position = positionsAfterPixelSnap[i] = PixelClamp(truePositions[i], obj.transform.lossyScale, ppus[i]);

            RepositionChildObjects(obj);
        }
        teleportedThisFrame = false;
        lastPosition = transform.position;
    }

    private void LoadChildObjects(GameObject obj) {
        float objectWidth = halfWidths[Array.IndexOf(children, obj)] * 2f;
        int childsNeeded = (int) Mathf.Ceil(ScreenBounds.x / objectWidth) + 1;
        GameObject clone = Instantiate(obj);
        for (int i = 0; i <= childsNeeded; i++) {
            GameObject c = Instantiate(clone);
            c.transform.SetParent(obj.transform);
            c.transform.position = new Vector3(objectWidth * i, obj.transform.position.y, obj.transform.position.z);
            c.name = obj.name + i;
        }
        Destroy(clone);
        if (obj.GetComponent<LegacyAnimateSpriteRenderer>() is LegacyAnimateSpriteRenderer anim)
            Destroy(anim);
        if (obj.GetComponent<SpriteRenderer>() is SpriteRenderer sRenderer)
            Destroy(sRenderer);
    }

    private void RepositionChildObjects(GameObject obj) {
        if (!obj)
            return;

        Transform parent = obj.transform;
        if (parent.childCount > 1) {
            GameObject firstChild = parent.GetChild(0).gameObject;
            GameObject lastChild = parent.GetChild(parent.childCount - 1).gameObject;
            float halfObjectWidth = halfWidths[Array.IndexOf(children, obj)];
            if (transform.position.x + ScreenBounds.x > lastChild.transform.position.x + halfObjectWidth) {
                firstChild.transform.SetAsLastSibling();
                firstChild.transform.position = new Vector3(lastChild.transform.position.x + halfObjectWidth * 2, lastChild.transform.position.y, lastChild.transform.position.z);
            } else if (transform.position.x - ScreenBounds.x < firstChild.transform.position.x - halfObjectWidth) {
                lastChild.transform.SetAsFirstSibling();
                lastChild.transform.position = new Vector3(firstChild.transform.position.x - halfObjectWidth * 2, firstChild.transform.position.y, firstChild.transform.position.z);
            }
        }
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
