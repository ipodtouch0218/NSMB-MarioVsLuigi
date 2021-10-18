using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TrackIcon : MonoBehaviour {
    [SerializeField] float trackMinX, trackMaxX, offsetY;
    public GameObject target;
    private PlayerController playerTarget;
    public Color outlineColor;
    Image image;
    public Sprite starSprite, playerSprite;
    private float flashTimer;
    void Start() {
        image = GetComponent<Image>();
    }

    void Update() {
        if (target == null) {
            Destroy(gameObject);
            return;
        }
        image.enabled = true;
        if (target.tag == "Player") {
            image.sprite = playerSprite;
            if (playerTarget == null) {
                playerTarget = target.GetComponent<PlayerController>();
            }
            if (playerTarget.dead) {
                flashTimer += Time.deltaTime;
                image.enabled = (flashTimer % 0.2f) <= 0.1f;
            } else {
                flashTimer = 0;
                image.enabled = true;
            }
        } else {
            image.sprite = starSprite;
        }
        float levelWidth = GameManager.Instance.GetLevelMaxX() - GameManager.Instance.GetLevelMinX();
        float trackWidth = trackMaxX - trackMinX;
        float percentage = (target.transform.position.x - GameManager.Instance.GetLevelMinX()) / levelWidth;
        transform.localPosition = new Vector2(percentage * trackWidth - trackMaxX, transform.localPosition.y);
    }
}
