using UnityEngine;
using UnityEngine.UI;

public class TrackIcon : MonoBehaviour {
    public float trackMinX, trackMaxX;
    public GameObject target;
    private PlayerController playerTarget;
    Image image;
    public bool doAnimation;
    public Sprite starSprite;
    private float flashTimer;
    private Material mat;

    void Start() {
        image = GetComponent<Image>();

        StarBouncer star;
        if ((star = target.GetComponent<StarBouncer>()) && star.stationary) {
            GetComponent<Animator>().enabled = true;
            transform.localScale = Vector2.zero;
        }

        mat = image.material;
        Update();
    }
    void Update() {
        if (target == null) {
            Destroy(gameObject);
            return;
        }
        image.enabled = true;
        if (target.CompareTag("Player")) {
            if (!playerTarget)
                playerTarget = target.GetComponent<PlayerController>();
            image.color = playerTarget.AnimationController.GlowColor;
            if (playerTarget.dead) {
                flashTimer += Time.deltaTime;
                image.enabled = (flashTimer % 0.2f) <= 0.1f;
            } else {
                flashTimer = 0;
                image.enabled = true;
            }
            transform.localScale = Vector3.one * (playerTarget.cameraController.controlCamera ? 1 : (2 / 3f));

            mat.SetColor("OverlayColor", playerTarget.AnimationController.GlowColor);
            mat.SetFloat("Star", playerTarget.invincible > 0 ? 1 : 0);
        } else {
            image.sprite = starSprite;
            image.enabled = true;
        }
        float levelWidth = GameManager.Instance.GetLevelMaxX() - GameManager.Instance.GetLevelMinX();
        float trackWidth = trackMaxX - trackMinX;
        float percentage = (target.transform.position.x - GameManager.Instance.GetLevelMinX()) / levelWidth;
        transform.localPosition = new Vector2(percentage * trackWidth - trackMaxX, transform.localPosition.y);
    }
}
