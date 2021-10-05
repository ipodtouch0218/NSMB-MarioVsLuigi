using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{

    float scroll;
    [SerializeField] float minY, maxY, centerWidth = 1f, minX = -1000, maxX = 1000;
    public GameObject target;

    // Start is called before the first frame update
    void Start() {
    }

    // Update is called once per frame
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
        } else if (ctp.x - currX > centerWidth) {
            targetX = ctp.x - centerWidth;
        } else if (ctp.x - currX < -centerWidth) {
            targetX = ctp.x + centerWidth;
        }

        float targetY = currY;
        if (ctp.y - currY > centerWidth) {
            targetY = ctp.y - centerWidth;
        } else if (ctp.y - currY < -centerWidth) {
            targetY = ctp.y + centerWidth;
        }
        
        targetPos = new Vector3(Mathf.Clamp(targetX, minX, maxX), Mathf.Clamp(targetY, minY + (Camera.main.orthographicSize - 4.7f), maxY), Camera.main.transform.position.z);

        Camera.main.transform.position = targetPos;
        scroll = Mathf.Clamp(Camera.main.transform.position.x - ctp.x, -centerWidth, centerWidth);
    }
}
