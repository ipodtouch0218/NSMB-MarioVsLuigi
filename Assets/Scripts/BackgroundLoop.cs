using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BackgroundLoop : MonoBehaviour {
    private GameObject[] levels;
    private Camera mainCamera;
    private Vector2 screenBounds;
    private Vector3 lastPosition;

    public static BackgroundLoop Instance = null;
    public bool wrap;

    void Start() {
        Instance = this;
        Transform t = GameObject.FindGameObjectWithTag("Backgrounds").transform;
        levels = new GameObject[t.childCount];
        for (int i = 0; i < t.childCount; i++)
            levels[i] = t.GetChild(i).gameObject;

        mainCamera = gameObject.GetComponent<Camera>();
        screenBounds = mainCamera.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, mainCamera.transform.position.z));
        foreach (GameObject obj in levels)
            LoadChildObjects(obj);

        lastPosition = transform.position;
    }
    void LoadChildObjects(GameObject obj) {
        float objectWidth = obj.GetComponent<SpriteRenderer>().bounds.size.x;
        int childsNeeded = (int) Mathf.Ceil(screenBounds.x * 2 / objectWidth) + 2;
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

        Transform[] children = obj.GetComponentsInChildren<Transform>();
        if (children.Length > 1) {
            GameObject firstChild = children[1].gameObject;
            GameObject lastChild = children[^1].gameObject;
            float halfObjectWidth = lastChild.GetComponent<SpriteRenderer>().bounds.extents.x;
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
        float difference = transform.position.x - lastPosition.x;

        if (wrap) {
            foreach (GameObject obj in levels) {
                obj.transform.Translate(difference, 0, 0);
                RepositionChildObjects(obj);
            }
            wrap = false;
        } else {
            foreach (GameObject obj in levels) {
                RepositionChildObjects(obj); 
                float parallaxSpeed = 1 - Mathf.Clamp01(Mathf.Abs(lastPosition.z / obj.transform.position.z));
                obj.transform.Translate(difference * parallaxSpeed * Vector3.right);
            }
        }
        lastPosition = transform.position;
    }
}