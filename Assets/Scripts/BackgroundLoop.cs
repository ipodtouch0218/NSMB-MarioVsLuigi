using System;
using UnityEngine;

using NSMB.Extensions;
using NSMB.Utils;

public class BackgroundLoop : MonoBehaviour {

    //---Static Variables
    public static BackgroundLoop Instance { get; private set; }
    private static readonly Vector2 ScreenBounds = new(7.5f, 5f);

    //---Public Variables
    public bool teleportedThisFrame;

    //---Misc Variables
    private GameObject[] children;
    private Vector3[] truePositions;
    private float[] ppus, halfWidths;
    private Vector3 lastPosition;

    public void Start() {
        Instance = this;

        Transform t = GameObject.FindGameObjectWithTag("Backgrounds").transform;

        children = new GameObject[t.childCount];
        ppus = new float[t.childCount];
        truePositions = new Vector3[t.childCount];
        halfWidths = new float[t.childCount];

        for (int i = 0; i < t.childCount; i++) {
            children[i] = t.GetChild(i).gameObject;
            SpriteRenderer sr = children[i].GetComponent<SpriteRenderer>();
            ppus[i] = sr.sprite.pixelsPerUnit;
            halfWidths[i] = sr.bounds.extents.x - 0.00004f;
            truePositions[i] = children[i].transform.position;
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
            float parallaxSpeed = 1 - Mathf.Clamp01(Mathf.Abs(lastPosition.z / obj.transform.position.z));
            Utils.WrappedDistance(transform.position, lastPosition, out float xDifference);
            float difference = xDifference + (obj.transform.position.x - truePositions[i].x);

            if (teleportedThisFrame) {
                truePositions[i].x += ((transform.position.x > GameManager.Instance.LevelMiddleX) ? 1 : -1) * GameManager.Instance.LevelWidth;
            }

            Vector3 newPosition = truePositions[i] + difference * parallaxSpeed * Vector3.right;
            truePositions[i] = newPosition;
            obj.transform.position = newPosition;


            RepositionChildObjects(obj);
        }
        teleportedThisFrame = false;
        lastPosition = transform.position;
    }

    private void LoadChildObjects(GameObject obj) {
        float objectWidth = halfWidths[Array.IndexOf(children, obj)] * 2f;
        int childsNeeded = (int) Mathf.Ceil(ScreenBounds.x * 2f / objectWidth);
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
            Debug.DrawRay(transform.position + Vector3.right.Multiply(ScreenBounds), Vector2.up, Color.green);
            Debug.DrawRay(transform.position - Vector3.right.Multiply(ScreenBounds), Vector2.up, Color.green);
            if (transform.position.x + ScreenBounds.x > lastChild.transform.position.x + halfObjectWidth) {
                firstChild.transform.SetAsLastSibling();
                firstChild.transform.position = new Vector3(lastChild.transform.position.x + halfObjectWidth * 2, lastChild.transform.position.y, lastChild.transform.position.z);
            } else if (transform.position.x - ScreenBounds.x < firstChild.transform.position.x - halfObjectWidth) {
                lastChild.transform.SetAsFirstSibling();
                lastChild.transform.position = new Vector3(firstChild.transform.position.x - halfObjectWidth * 2, firstChild.transform.position.y, firstChild.transform.position.z);
            }
        }
    }
}
