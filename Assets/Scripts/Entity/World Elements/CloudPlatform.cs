using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using Fusion;
using NSMB.Utils;

public class CloudPlatform : SimulationBehaviour {

    //---Static Variables
    private static readonly Collider2D[] CollisionBuffer = new Collider2D[32];
    private static readonly int ParamDisplacementMap = Shader.PropertyToID("DisplacementMap");
    private static readonly int ParamPlatformWidth = Shader.PropertyToID("PlatformWidth");

    //---Serialized Variables
    [SerializeField] private EdgeCollider2D ground;
    [SerializeField] private BoxCollider2D trigger;
    [SerializeField, Delayed] private int platformWidth = 8, samplesPerTile = 8;
    [SerializeField] private float time = 0.25f;
    [SerializeField] private bool changeCollider = true;

    //---Private Variables
    private readonly List<CloudContact> positions = new();
    private Color32[] pixels;
    private MaterialPropertyBlock mpb;
    private Texture2D displacementMap;
    private SpriteRenderer spriteRenderer;

    public void Start() {
        Initialize();
    }

    public void OnValidate() {
        ValidationUtility.SafeOnValidate(Initialize);
    }

    private void Initialize() {
        if (this == null || !this) {
            //what
            return;
        }

        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.size = new(platformWidth * 0.5f, 1f);

        if (changeCollider) {
            ground.SetPoints(new Vector2[] { Vector2.zero, new(platformWidth * 0.5f, 0) }.ToList());
            ground.offset = new(0, 5/16f);

            trigger.size = new(platformWidth * 0.5f, 0.5f - 5/16f);
            trigger.offset = new(platformWidth * 0.25f, 13/32f);
        }

        displacementMap = new(platformWidth * samplesPerTile, 1) {
            wrapMode = TextureWrapMode.Mirror
        };

        pixels = new Color32[platformWidth * samplesPerTile];
        for (int i = 0; i < platformWidth * samplesPerTile; i++) {
            pixels[i] = new Color32(0, 0, 0, 255);
        }

        displacementMap.SetPixels32(pixels);
        displacementMap.Apply();

        mpb = new();
        spriteRenderer.GetPropertyBlock(mpb);
        mpb.SetFloat(ParamPlatformWidth, platformWidth);
        spriteRenderer.SetPropertyBlock(mpb);
    }

    public override void Render() {

        // Update collisions
        int collisionCount = Runner.GetPhysicsScene2D().OverlapBox((Vector2) transform.position + trigger.offset, trigger.size, 0, CollisionBuffer, Layers.MaskEntities);
        for (int i = 0; i < collisionCount; i++) {
            var obj = CollisionBuffer[i];

            HandleTrigger(obj);
        }

        // Update collision timers
        foreach (CloudContact contact in positions) {
            if (!contact.Exit && (!contact.Collider || !contact.Mover.Data.OnGround || !Utils.BufferContains(CollisionBuffer, collisionCount, contact.Collider))) {
                contact.Exit = true;
            }

            if (contact.Exit) {
                contact.Timer += Time.deltaTime * 0.333f;
            } else {
                contact.Timer = Mathf.Max(0, contact.Timer - Time.deltaTime);
            }
        }

        // Purge old collisions
        for (int i = positions.Count - 1; i >= 0; i--) {
            CloudContact contact = positions[i];
            if (!contact.Collider || (contact.Exit && contact.Timer >= time)) {
                positions.RemoveAt(i);
            }
        }

        // Handle graphics
        for (int i = 0; i < platformWidth * samplesPerTile; i++) {
            pixels[i].r = 0;
        }

        foreach (CloudContact contact in positions) {

            float percentageCompleted = 1f - (contact.Timer / time);
            float v = Mathf.Sin(Mathf.PI * 0.5f * percentageCompleted);

            int point = contact.Point;
            int width = contact.Width;
            for (int x = -width; x <= width; x++) {
                float color = v;
                int localPoint = point + x;
                if (localPoint < 0 || localPoint >= platformWidth * samplesPerTile) {
                    continue;
                }

                color *= Mathf.SmoothStep(1, 0, ((float) Mathf.Abs(x)) / width);
                byte final = (byte) (Mathf.Clamp01(color) * 255);

                if (pixels[localPoint].r > final) {
                    continue;
                }

                pixels[localPoint].r = final;
            }
        }

        displacementMap.SetPixels32(pixels);
        displacementMap.Apply();

        mpb.SetTexture(ParamDisplacementMap, displacementMap);
        spriteRenderer.SetPropertyBlock(mpb);
    }

    private void HandleTrigger(Collider2D collision) {
        EntityMover mover = collision.GetComponentInParent<EntityMover>();
        if (!mover.Data.OnGround) {
            return;
        }

        if (GetContact(collision) == null) {
            positions.Add(new(this, mover, collision as BoxCollider2D));
        }
    }

    private CloudContact GetContact(Collider2D collider) {
        foreach (CloudContact contact in positions) {
            if (contact.Exit) {
                continue;
            }

            if (contact.Collider == collider) {
                return contact;
            }
        }
        return null;
    }

    public class CloudContact {
        public EntityMover Mover;
        public CloudPlatform Platform;
        public float Timer;
        public bool Exit;
        public int LastPoint;
        public int Point {
            get {
                if (Exit) {
                    return LastPoint;
                }

                return LastPoint = (int) (Platform.transform.InverseTransformPoint(Collider.transform.position).x * Platform.samplesPerTile);
            }
        }
        public BoxCollider2D Collider;
        public int Width {
            get {
                if (Collider) {
                    return (int) (Collider.size.x * Collider.transform.lossyScale.x * 4f * Platform.samplesPerTile);
                }

                return (int) (1.75f * Platform.samplesPerTile);
            }
        }
        public CloudContact(CloudPlatform platform, EntityMover mover, BoxCollider2D collider) {
            Platform = platform;
            Mover = mover;
            Collider = collider;
            Timer = 0.05f;
        }
    }
}
