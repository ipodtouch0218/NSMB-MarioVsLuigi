using UnityEngine;

[RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
public class WaterSplash : MonoBehaviour {

    [Delayed]
    public int widthTiles = 64, pointsPerTile = 8, splashWidth = 2;
    [Delayed]
    public float heightTiles = 1;
    public float tension = 40, kconstant = 1.5f, damping = 0.92f, splashVelocity = 50f, resistance = 0f;
    public string splashParticle;
    private Texture2D heightTex;
    private float[] pointHeights, pointVelocities;
    private Color32[] colors;

    private int totalPoints;
    private SpriteRenderer spriteRenderer;
    MaterialPropertyBlock properties;
    private bool initialized;

    private void Awake() {
        Initialize();
    }
    private void OnValidate() {
        ValidationUtility.SafeOnValidate(() => {
            Initialize();
        });
    }

    private void Initialize() {
        if (this == null)
            return;

        BoxCollider2D collider = GetComponent<BoxCollider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        totalPoints = widthTiles * pointsPerTile;
        pointHeights = new float[totalPoints];
        pointVelocities = new float[totalPoints];
        colors = new Color32[totalPoints];

        heightTex = new Texture2D(totalPoints, 1);
        for (int i = 0; i < totalPoints; i++)
            colors[i] = new Color(.5f, 0, 0, 1);
        heightTex.SetPixels32(colors);
        heightTex.Apply();

        collider.offset = new(0, heightTiles * 0.25f);
        collider.size = new(widthTiles * 0.5f, heightTiles * 0.5f);
        spriteRenderer.size = new(widthTiles * 0.5f, heightTiles * 0.5f + 0.5f);

        properties = new();
        properties.SetTexture("Heightmap", heightTex);
        properties.SetFloat("WidthTiles", widthTiles);
        properties.SetFloat("Height", heightTiles);
        spriteRenderer.SetPropertyBlock(properties);
    }

    void FixedUpdate() {
        if (!initialized) {
            Initialize();
            initialized = true;
        }
        float delta = Time.fixedDeltaTime;

        for (int i = 0; i < totalPoints; i++) {
            float height = pointHeights[i];
            pointVelocities[i] += tension * -height;
            pointVelocities[i] *= damping;
        }
        for (int i = 0; i < totalPoints; i++) {
            pointHeights[i] += pointVelocities[i] * delta;
        }
        for (int i = 0; i < totalPoints; i++) {
            float height = pointHeights[i];

            pointVelocities[i] -= kconstant * delta * (height - pointHeights[(i + totalPoints - 1) % totalPoints]); //left
            pointVelocities[i] -= kconstant * delta * (height - pointHeights[(i + totalPoints + 1) % totalPoints]); //right
        }
        for (int i = 0; i < totalPoints; i++) {
            colors[i].r = (byte) ((Mathf.Clamp(pointHeights[i] / 20f, -0.5f, 0.5f) + 0.5f) * 255);
        }

        heightTex.SetPixels32(colors, 0);
        heightTex.Apply();
    }
    void OnTriggerEnter2D(Collider2D collider) {
        Instantiate(Resources.Load(splashParticle), collider.transform.position, Quaternion.identity);

        Rigidbody2D body = collider.attachedRigidbody;
        float power = body ? body.velocity.y : -1;
        float tile = (transform.InverseTransformPoint(collider.transform.position).x / widthTiles + 0.25f) * 2f;
        int px = (int) (tile * totalPoints);
        for (int i = -splashWidth; i <= splashWidth; i++) {
            int pointsX = (px + totalPoints + i) % totalPoints;
            pointVelocities[pointsX] = -splashVelocity * power;
        }
    }
    void OnTriggerStay2D(Collider2D collision) {
        if (collision.attachedRigidbody == null)
            return;

        collision.attachedRigidbody.velocity *= 1-Mathf.Clamp01(resistance);
    }
}
