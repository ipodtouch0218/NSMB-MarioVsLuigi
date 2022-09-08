using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CloudPlatform : MonoBehaviour {

    [Delayed]
    public int platformWidth = 8, samplesPerTile = 8;
    public float time = 0.25f;
    public bool changeCollider = true;

    public EdgeCollider2D ground;
    public BoxCollider2D trigger;

    [SerializeField] private List<CloudContact> positions = new();

    private MaterialPropertyBlock mpb;
    private Texture2D displacementMap;
    private SpriteRenderer spriteRenderer;
    private Color32[] pixels;

    public void Start() {
        Initialize();
    }

    public void OnValidate() {
        ValidationUtility.SafeOnValidate(() => {
            Initialize();
        });
    }

    private void Initialize() {
        if (this == null)
            //what
            return;

        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.size = new(platformWidth / 2f, 1f);

        if (changeCollider) {
            ground.SetPoints(new Vector2[] { Vector2.zero, new(platformWidth / 2f, 0) }.ToList());
            ground.offset = new(0, 5/16f);

            trigger.size = new(platformWidth / 2f, 0.5f - 5/16f);
            trigger.offset = new(platformWidth / 4f, 13/32f);
        }

        displacementMap = new(platformWidth * samplesPerTile, 1);
        displacementMap.wrapMode = TextureWrapMode.Mirror;

        pixels = new Color32[platformWidth * samplesPerTile];
        for (int i = 0; i < platformWidth * samplesPerTile; i++)
            pixels[i] = new Color32(0, 0, 0, 255);
        displacementMap.SetPixels32(pixels);
        displacementMap.Apply();

        mpb = new();
        spriteRenderer.GetPropertyBlock(mpb);

        mpb.SetFloat("PlatformWidth", platformWidth);
        spriteRenderer.SetPropertyBlock(mpb);
    }

    public void Update() {
        for (int i = 0; i < platformWidth * samplesPerTile; i++)
            pixels[i].r = 0;

        for (int i = 0; i < positions.Count; i++) {
            CloudContact contact = positions[i];

            if (contact.obj == null) {
                positions.RemoveAt(i--);
                continue;
            }

            if (!contact.exit && contact.obj.velocity.y > 0.2f)
                contact.exit = true;

            if (contact.exit) {
                contact.timer += Time.deltaTime / 3f;
                if (contact.timer >= time) {
                    positions.RemoveAt(i--);
                    continue;
                }
            } else {
                contact.timer = Mathf.Max(0, contact.timer - Time.deltaTime);
            }

            float percentageCompleted = 1f - (contact.timer / time);
            float v = Mathf.Sin(Mathf.PI / 2f * percentageCompleted);

            int point = contact.Point;
            int width = contact.Width;
            for (int x = -width; x <= width; x++) {
                float color = v;
                int localPoint = point + x;
                if (localPoint < 0 || localPoint >= platformWidth * samplesPerTile)
                    continue;

                color *= Mathf.SmoothStep(1, 0, ((float) Mathf.Abs(x)) / width);
                byte final = (byte) (Mathf.Clamp01(color) * 255);

                if (pixels[localPoint].r > final)
                    continue;

                pixels[localPoint].r = final;
            }
        }

        displacementMap.SetPixels32(pixels);
        displacementMap.Apply();

        mpb.SetTexture("DisplacementMap", displacementMap);
        spriteRenderer.SetPropertyBlock(mpb);
    }

    public void OnTriggerEnter2D(Collider2D collision) {
        HandleTrigger(collision);
    }

    public void OnTriggerStay2D(Collider2D collision) {
        HandleTrigger(collision);
    }

    public void OnTriggerExit2D(Collider2D collision) {
        StartCoroutine(AttemptRemove(collision));
    }

    IEnumerator AttemptRemove(Collider2D collision) {
        yield return null;
        yield return null;
        CloudContact contact = GetContact(collision.attachedRigidbody);
        if (contact != null)
            contact.exit = true;
    }

    private void HandleTrigger(Collider2D collision) {
        Rigidbody2D rb = collision.attachedRigidbody;
        if (rb.isKinematic || rb.velocity.y > 0.2f || rb.position.y < transform.position.y)
            return;

        if (GetContact(collision.attachedRigidbody) == null)
            positions.Add(new(this, collision.attachedRigidbody, collision as BoxCollider2D));
    }

    private CloudContact GetContact(Rigidbody2D body) {
        foreach (CloudContact contact in positions) {
            if (contact.exit)
                continue;

            if (contact.obj.gameObject == body.gameObject)
                return contact;
        }
        return null;
    }

    [System.Serializable]
    public class CloudContact {
        public Rigidbody2D obj;
        public CloudPlatform platform;
        public float timer;
        public bool exit;
        public int lastPoint;
        public int Point {
            get {
                if (exit)
                    return lastPoint;
                return lastPoint = (int) (platform.transform.InverseTransformPoint(obj.transform.position).x * platform.samplesPerTile);
            }
        }
        private BoxCollider2D collider;
        public int Width {
            get {
                if (collider)
                    return (int) (collider.size.x * collider.transform.lossyScale.x * 4f * platform.samplesPerTile);
                return (int) (1.75f * platform.samplesPerTile);
            }
        }
        public CloudContact(CloudPlatform platform, Rigidbody2D obj, BoxCollider2D collider) {
            this.platform = platform;
            this.obj = obj;
            this.collider = collider;
            timer = 0.05f;
        }
    }
}
