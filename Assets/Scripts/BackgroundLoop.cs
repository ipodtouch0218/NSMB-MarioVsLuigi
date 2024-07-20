using System;
using UnityEngine;

using NSMB.Extensions;
using Photon.Deterministic;
using Quantum;

public class BackgroundLoop : QuantumCallbacks {

    //---Static Variables
    public static BackgroundLoop Instance { get; private set; }
    private static readonly Vector2 ScreenBounds = new(7.5f, 5f);

    //---Misc Variables
    private GameObject[] children;
    private Vector3[] truePositions;
    private float[] ppus, halfWidths;
    private VersusStageData stage;
    private FPVector2 lastPosition;
    private Transform cameraTransform;

    public void Start() {
        Instance = this;
        cameraTransform = Camera.main.transform;

        children = new GameObject[transform.childCount];
        ppus = new float[transform.childCount];
        truePositions = new Vector3[transform.childCount];
        halfWidths = new float[transform.childCount];

        for (int i = 0; i < transform.childCount; i++) {
            children[i] = transform.GetChild(i).gameObject;
            SpriteRenderer sr = children[i].GetComponent<SpriteRenderer>();
            ppus[i] = sr.sprite.pixelsPerUnit;
            halfWidths[i] = sr.bounds.extents.x - 0.00004f;
            truePositions[i] = children[i].transform.position;
        }

        foreach (GameObject obj in children) {
            LoadChildObjects(obj);
        }

        lastPosition = cameraTransform.position.ToFPVector2();
        stage = (VersusStageData) QuantumUnityDB.GetGlobalAsset(FindObjectOfType<QuantumMapData>().Asset.UserAsset);
    }

    public void LateUpdate() {
        Reposition();
    }

    public void Reposition() {
        if (!stage) {
            return;
        }

        QuantumUtils.WrappedDistance(stage, cameraTransform.position.ToFPVector2(), lastPosition, out FP xDifference);
        float absoluteDifference = cameraTransform.position.x - lastPosition.X.AsFloat;

        for (int i = 0; i < children.Length; i++) {
            GameObject obj = children[i];
            float parallaxSpeed = 1 - Mathf.Clamp01(Mathf.Abs(-10f / obj.transform.position.z));

            float difference = xDifference.AsFloat + (obj.transform.position.x - truePositions[i].x);

            if (Mathf.Abs(absoluteDifference) > 2) {
                truePositions[i].x += ((cameraTransform.position.x > stage.StageWorldMin.X.AsFloat + (stage.TileDimensions.x * 0.25f)) ? 1 : -1) * (stage.TileDimensions.x * 0.5f);
            }

            if (parallaxSpeed > 0) {
                Vector3 newPosition = truePositions[i] + difference * parallaxSpeed * Vector3.right;
                truePositions[i] = newPosition;
                obj.transform.position = newPosition;
            }

            RepositionChildObjects(obj);
        }
        lastPosition = cameraTransform.position.ToFPVector2();
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
        if (obj.GetComponent<LegacyAnimateSpriteRenderer>() is LegacyAnimateSpriteRenderer anim) {
            Destroy(anim);
        }

        if (obj.GetComponent<SpriteRenderer>() is SpriteRenderer sRenderer) {
            Destroy(sRenderer);
        }
    }

    private void RepositionChildObjects(GameObject obj) {
        if (!obj) {
            return;
        }

        Transform parent = obj.transform;
        if (parent.childCount > 1) {
            GameObject firstChild = parent.GetChild(0).gameObject;
            GameObject lastChild = parent.GetChild(parent.childCount - 1).gameObject;
            float halfObjectWidth = halfWidths[Array.IndexOf(children, obj)];
            Debug.DrawRay(cameraTransform.position + Vector3.right.Multiply(ScreenBounds), Vector2.up, Color.green);
            Debug.DrawRay(cameraTransform.position - Vector3.right.Multiply(ScreenBounds), Vector2.up, Color.green);
            while (cameraTransform.position.x + ScreenBounds.x > lastChild.transform.position.x + halfObjectWidth) {
                firstChild.transform.SetAsLastSibling();
                firstChild.transform.position = new Vector3(lastChild.transform.position.x + halfObjectWidth * 2, lastChild.transform.position.y, lastChild.transform.position.z);
                firstChild = parent.GetChild(0).gameObject;
                lastChild = parent.GetChild(parent.childCount - 1).gameObject;
            }
            while (cameraTransform.position.x - ScreenBounds.x < firstChild.transform.position.x - halfObjectWidth) {
                lastChild.transform.SetAsFirstSibling();
                lastChild.transform.position = new Vector3(firstChild.transform.position.x - halfObjectWidth * 2, firstChild.transform.position.y, firstChild.transform.position.z);
                firstChild = parent.GetChild(0).gameObject;
                lastChild = parent.GetChild(parent.childCount - 1).gameObject;
            }
        }
    }
}