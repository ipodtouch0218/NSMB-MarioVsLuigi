using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TrackIcon : MonoBehaviour
{
    [SerializeField] float trackMinX, trackMaxX, offsetY;
    public bool star;
    Image image;
    void Start(){
        image = GetComponent<Image>();
    }

    void Update() {
        GameObject target = null;
        if (star) {
            foreach (StarBouncer sb in GameObject.FindObjectsOfType<StarBouncer>()) {
                if (sb.stationary) {
                    target = sb.gameObject;
                    break;
                }
            }
        } else {  
            target = GameManager.Instance.localPlayer;
        }
        if (target == null) {
            image.enabled = false;
            return;
        }
        image.enabled = true;
        float levelWidth = GameManager.Instance.GetLevelMaxX() - GameManager.Instance.GetLevelMinX();
        float trackWidth = trackMaxX - trackMinX;
        float percentage = (target.transform.position.x - GameManager.Instance.GetLevelMinX()) / levelWidth;
        transform.localPosition = new Vector2(percentage * trackWidth - trackMaxX, transform.localPosition.y);
    }
}
