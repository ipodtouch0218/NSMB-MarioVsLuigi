using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour {

    float scroll;
    [SerializeField] float minY, maxY, centerXWidth = 0.5f, centerYWidth = 0.5f, minX = -1000, maxX = 1000;
    public GameObject target;
    
    void Update() {
        if (target == null)
            return;

        // height = Camera.main.orthographicSize * 2;
        // width = height * Camera.main.aspect;
        Vector3 ctp = target.transform.position;

        Vector3 targetPos;
        float currX = Camera.main.transform.position.x;
        float currY = Camera.main.transform.position.y;
        
        float tp = ctp.x - currX; 
        float targetX = currX;

        if (Mathf.Abs(tp) > 5) {
            targetX = ctp.x + scroll;
        } else if (ctp.x - currX > centerXWidth) {
            targetX = ctp.x - centerXWidth;
        } else if (ctp.x - currX < -centerXWidth) {
            targetX = ctp.x + centerXWidth;
        }

        float targetY = currY;
        if (ctp.y - currY > centerYWidth) {
            targetY = ctp.y - centerYWidth;
        } else if (ctp.y - currY < -centerYWidth) {
            targetY = ctp.y + centerYWidth;
        }
        
        targetPos = new Vector3(Mathf.Clamp(targetX, minX, maxX), Mathf.Clamp(targetY, minY + (Camera.main.orthographicSize - 4.7f), maxY), Camera.main.transform.position.z);

        Camera.main.transform.position = targetPos;
        scroll = Mathf.Clamp(Camera.main.transform.position.x - ctp.x, -centerXWidth, centerXWidth);
    }
}
