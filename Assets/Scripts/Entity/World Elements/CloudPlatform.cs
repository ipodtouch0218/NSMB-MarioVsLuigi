using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CloudPlatform : MonoBehaviour {

    [Delayed]
    public int platformWidth = 8, samplesPerTile = 8, blurPoints = 6;
    public float time = 0.25f;
    public bool changeCollider = true;

    public EdgeCollider2D ground;
    public BoxCollider2D trigger;

    private readonly List<CloudContact> positions = new();

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
            ground.offset = new(0, 1/4f);

            trigger.size = new(platformWidth / 2f, 1/4f);
            trigger.offset = new(platformWidth / 4f, 3/8f);
        }

        displacementMap = new(platformWidth * samplesPerTile, 1);

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
                return;
            }

            if (contact.obj.velocity.y > 0.2f)
                contact.exit = true;

            if (contact.exit) {
                contact.timer += Time.deltaTime / 3f;
                if (contact.timer >= time) {
                    positions.RemoveAt(i--);
                    return;
                }
            } else {
                contact.timer = Mathf.Max(0, contact.timer - Time.deltaTime);
            }

            float percentageCompleted = 1f - (contact.timer / time);
            float v = Mathf.Sin(Mathf.PI / 2f * percentageCompleted);

            int point = contact.Point;
            for (int x = -blurPoints; x <= blurPoints; x++) {
                float color = v;
                int localPoint = point + x;
                if (localPoint < 0 || localPoint >= platformWidth * samplesPerTile)
                    continue;

                color *= Mathf.SmoothStep(1, 0, ((float) Mathf.Abs(x)) / blurPoints);
                byte final = (byte) (Mathf.Clamp01(color) * 255);

                if (pixels[localPoint].r >= final)
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

    private void HandleTrigger(Collider2D collision) {
        Rigidbody2D rb = collision.attachedRigidbody;
        if (rb.velocity.y > 0.2f)
            return;

        CloudContact contact = GetContact(collision.attachedRigidbody);
        if (contact == null || contact.exit) {
            if ((collision.gameObject.GetComponent<PlayerController>()?.state ?? Enums.PowerupState.None) != Enums.PowerupState.MiniMushroom)
                positions.Add(new(this, collision.attachedRigidbody));
        }
    }

    public void OnTriggerExit2D(Collider2D collision) {
        CloudContact contact = GetContact(collision.attachedRigidbody);
        if (contact != null)
            contact.exit = true;
    }


    private CloudContact GetContact(Rigidbody2D body) {
        foreach (CloudContact contact in positions) {
            if (contact.obj == body)
                return contact;
        }
        return null;
    }

    class CloudContact {
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
        public CloudContact(CloudPlatform platform, Rigidbody2D obj) {
            this.platform = platform;
            this.obj = obj;
            timer = platform.time;
        }
    }
}
