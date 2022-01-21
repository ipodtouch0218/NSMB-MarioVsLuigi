using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class CameraController : MonoBehaviour {

    float scroll;
    [SerializeField] public float minY, heightY, centerXWidth = 0.5f, centerYWidth = 0.5f, minX = -1000, maxX = 1000;
    public GameObject target;
    public Vector3 targetOffset = Vector3.zero;
    public bool exactCentering = false;
    public Vector3 dampVelocity = new Vector3();

    void Update() {
        if (target == null)
            return;
        
        Vector3 ctp = target.transform.position + targetOffset;
        Vector3 targetPos = ctp;
        targetPos.z = transform.position.z;
        if (!exactCentering) {
            float currX = transform.position.x;
            float currY = transform.position.y;
            
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

            targetPos.x = targetX;
            targetPos.y = targetY;
        }

        float vOrtho = Camera.main.orthographicSize;
        float hOrtho = vOrtho * (Camera.main.aspect);
        targetPos.x = Mathf.Clamp(targetPos.x, minX + hOrtho, maxX - hOrtho);
        targetPos.y = Mathf.Clamp(targetPos.y, minY + vOrtho, (heightY == 0 ? (minY + vOrtho) : (minY + heightY - vOrtho)));
        
        if (exactCentering) {
            if (Vector2.Distance(transform.position, targetPos) > 10) {
                bool right = transform.position.x < targetPos.x;
                transform.position += new Vector3(((right ? 1 : -1) * GameManager.Instance.levelWidthTile/2f), 0, 0);
            } 
            Vector3 result = Vector3.SmoothDamp(transform.position, targetPos, ref dampVelocity, 0.5f);
            result.y = targetPos.y;
            transform.position = result;
        } else {
            transform.position = targetPos;
        }
        scroll = Mathf.Clamp(transform.position.x - ctp.x, -centerXWidth, centerXWidth);
    }
}
