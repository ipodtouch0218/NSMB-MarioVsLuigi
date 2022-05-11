using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;

public class TrackIcon : MonoBehaviour {
    public float trackMinX, trackMaxX;
    public GameObject target;
    private PlayerController playerTarget;
    public Color outlineColor;
    Image image;
    public bool doAnimation;
    public Sprite starSprite;
    private float flashTimer;
    void Start() {
        image = GetComponent<Image>();

        StarBouncer star;
        if (star = target.GetComponent<StarBouncer>())
            GetComponent<Animator>().enabled = star.stationary;
    }
    void Update() {
        if (target == null) {
            Destroy(gameObject);
            return;
        }
        image.enabled = true;
        if (target.CompareTag("Player")) {
            image.sprite = Utils.GetCharacterData(target.GetPhotonView().Owner).trackSprite;
            if (!playerTarget)
                playerTarget = target.GetComponent<PlayerController>();
            if (playerTarget.dead) {
                flashTimer += Time.deltaTime;
                image.enabled = (flashTimer % 0.2f) <= 0.1f;
            } else {
                flashTimer = 0;
                image.enabled = true;
            }
            transform.localScale = Vector3.one * (playerTarget.cameraController.controlCamera ? 1 : (2 / 3f));
        } else {
            image.sprite = starSprite;
        }
        float levelWidth = GameManager.Instance.GetLevelMaxX() - GameManager.Instance.GetLevelMinX();
        float trackWidth = trackMaxX - trackMinX;
        float percentage = (target.transform.position.x - GameManager.Instance.GetLevelMinX()) / levelWidth;
        transform.localPosition = new Vector2(percentage * trackWidth - trackMaxX, transform.localPosition.y);
    }
}
