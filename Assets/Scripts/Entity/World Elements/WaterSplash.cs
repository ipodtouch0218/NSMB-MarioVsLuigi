using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using Fusion;

[RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
public class WaterSplash : NetworkBehaviour {

    //---Static Variables
    private static readonly Collider2D[] CollisionBuffer = new Collider2D[32];

    //---Serialized Variables
    [SerializeField] private GameObject splashPrefab;
    [Delayed] [SerializeField] private int widthTiles = 64, pointsPerTile = 8, splashWidth = 2;
    [Delayed] [SerializeField] private float heightTiles = 1;
    [SerializeField] private float tension = 40, kconstant = 1.5f, damping = 0.92f, splashVelocity = 50f, animationSpeed = 1f;
    [SerializeField] private bool fireDeath;

    //---Private Variables
    private readonly HashSet<GameObject> splashedEntities = new();
    private Texture2D heightTex;
    private SpriteRenderer spriteRenderer;
    private MaterialPropertyBlock properties;
    private BoxCollider2D hitbox;
    private Color32[] colors;
    private float[] pointHeights, pointVelocities;
    private float animTimer;
    private int totalPoints;
    private bool initialized;

    private void Awake() {
        Initialize();
    }

    private void OnValidate() {
        ValidationUtility.SafeOnValidate(Initialize);
    }

    private void Initialize() {
        if (this == null)
            return;

        hitbox = GetComponent<BoxCollider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        totalPoints = widthTiles * pointsPerTile;
        pointHeights = new float[totalPoints];
        pointVelocities = new float[totalPoints];

        heightTex = new Texture2D(totalPoints, 1, TextureFormat.RGBA32, false);

        Color32 gray = new(128, 0, 0, 255);
        colors = new Color32[totalPoints];
        for (int i = 0; i < totalPoints; i++)
            colors[i] = gray;

        heightTex.Apply();

        hitbox.offset = new(0, heightTiles * 0.25f - 0.2f);
        hitbox.size = new(widthTiles * 0.5f, heightTiles * 0.5f - 0.1f);
        spriteRenderer.size = new(widthTiles * 0.5f, heightTiles * 0.5f + 0.5f);

        properties = new();
        properties.SetTexture("Heightmap", heightTex);
        properties.SetFloat("WidthTiles", widthTiles);
        properties.SetFloat("Height", heightTiles);
        spriteRenderer.SetPropertyBlock(properties);
    }

    public void FixedUpdate() {
        if (!initialized) {
            Initialize();
            initialized = true;
        }
        float delta = Time.fixedDeltaTime;

        bool valuesChanged = false;

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
            byte newR = (byte) (Mathf.Clamp01((pointHeights[i] / 20f) + 0.5f) * 255f);
            valuesChanged |= colors[i].r != newR;
            colors[i].r = newR;
        }

        if (valuesChanged) {
            heightTex.SetPixels32(colors);
            heightTex.Apply(false);
        }

        animTimer += animationSpeed * Time.fixedDeltaTime;
        animTimer %= 8;
        properties.SetFloat("TextureIndex", animTimer);
        spriteRenderer.SetPropertyBlock(properties);
    }

    public override void FixedUpdateNetwork() {
        //find entities inside our collider
        int count = Runner.GetPhysicsScene2D().OverlapBox((Vector2) transform.position + hitbox.offset, hitbox.size, 0, CollisionBuffer);

        for (int i = 0; i < count; i++) {
            var obj = CollisionBuffer[i];
            if (!obj || obj.GetComponentInParent<BasicEntity>() is not BasicEntity entity)
                continue;

            //player collisions are special
            if (entity is PlayerController player) {
                if (player.IsDead)
                    continue;

                if (Runner.IsServer)
                    Rpc_Splash(entity.body.position, -Mathf.Abs(Mathf.Min(-3, entity.body.velocity.y)));

                player.Death(false, fireDeath);
                continue;
            }

            if (Runner.IsServer) {
                if (!splashedEntities.Contains(obj.gameObject)) {
                    Rpc_Splash(entity.body.position, -Mathf.Abs(Mathf.Min(-3, entity.body.velocity.y)));
                    splashedEntities.Add(obj.gameObject);
                }

                //kill entity if they're below the surface of the posion/lava
                if (entity.GetComponentInChildren<Renderer>().bounds.max.y < transform.position.y + heightTiles * 0.5f - 0.1f) {
                    //dont let fireballs "poof"
                    if (entity is FireballMover fm)
                        fm.PlayBreakEffect = false;

                    entity.Destroy(BasicEntity.DestroyCause.Lava);
                }
            }
        }

        splashedEntities.IntersectWith(CollisionBuffer.Take(count).Select(c => c.gameObject));
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void Rpc_Splash(Vector2 position, float power) {
        Instantiate(splashPrefab, position, Quaternion.identity);

        float tile = (transform.InverseTransformPoint(position).x / widthTiles + 0.25f) * 2f;
        int px = (int) (tile * totalPoints);
        for (int i = -splashWidth; i <= splashWidth; i++) {
            int pointsX = (px + totalPoints + i) % totalPoints;
            pointVelocities[pointsX] = -splashVelocity * power;
        }
    }
}
