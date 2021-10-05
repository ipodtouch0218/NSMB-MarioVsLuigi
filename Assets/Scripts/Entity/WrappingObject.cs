using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WrappingObject : MonoBehaviour
{
    [SerializeField] float offset;
    void Update() {
        float leftX = GameManager.Instance.GetLevelMinX();
        float rightX = GameManager.Instance.GetLevelMaxX();
        float width = (rightX - leftX);

        leftX = leftX + (width * offset);

        if (transform.position.x < leftX) {
            transform.position += new Vector3(width, 0, 0);
        }
        if (transform.position.x > rightX) {
            transform.position += new Vector3(-width, 0, 0);
        }
    }
}
