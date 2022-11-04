using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class TrackIcon : MonoBehaviour {

    public float trackMinX, trackMaxX;
    public GameObject target;
    public bool doAnimation;
    public Sprite starSprite;

    private PlayerController playerTarget;
    private Material mat;
    private Image image;
    private Coroutine flashRoutine;
    private bool changedSprite = false;

    public void Start() {
        image = GetComponent<Image>();

        StarBouncer star;
        if ((star = target.GetComponent<StarBouncer>()) && star.IsStationary) {
            GetComponent<Animator>().enabled = true;
            transform.localScale = Vector2.zero;
        }

        mat = image.material;
        Update();
    }

    public void Update() {
        if (target == null) {
            Destroy(gameObject);
            return;
        }

        if (playerTarget || target.CompareTag("Player")) {
            if (!playerTarget) {
                playerTarget = target.GetComponent<PlayerController>();
                if (!playerTarget)
                    return;

                image.color = playerTarget.animationController.GlowColor;
                mat.SetColor("OverlayColor", playerTarget.animationController.GlowColor);
            }

            flashRoutine ??= StartCoroutine(Flash());
            transform.localScale = playerTarget.cameraController.IsControllingCamera ? new(1, -1, 1) : Vector3.one * (2f / 3f);
        } else if (!changedSprite) {
            image.sprite = starSprite;
            image.enabled = true;
            changedSprite = true;
        }

        GameManager gm = GameManager.Instance;
        float levelWidth = gm.levelWidthTile * 0.5f;
        float trackWidth = trackMaxX - trackMinX;
        float percentage = (target.transform.position.x - gm.GetLevelMinX()) / levelWidth;
        transform.localPosition = new(percentage * trackWidth - trackMaxX, transform.localPosition.y);
    }

    private IEnumerator Flash() {
        bool flash = true;
        while (playerTarget.IsDead) {
            flash = !flash;
            image.enabled = flash;
            yield return new WaitForSeconds(0.1f);
        }

        image.enabled = true;
        flashRoutine = null;
    }
}
