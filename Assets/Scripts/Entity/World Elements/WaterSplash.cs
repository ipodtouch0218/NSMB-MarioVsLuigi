using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using Fusion;

[RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
[OrderAfter(typeof(NetworkPhysicsSimulation2D))]
public class WaterSplash : NetworkBehaviour {

    //---Serialized Variables
    [SerializeField] private GameObject splashPrefab, splashExitPrefab;
    [SerializeField] private SpriteMask mask;
    [Delayed] [SerializeField] private int widthTiles = 64, pointsPerTile = 8, splashWidth = 2;
    [Delayed] [SerializeField] private float heightTiles = 1;
    [SerializeField] private float tension = 40, kconstant = 1.5f, damping = 0.92f, splashVelocity = 50f, animationSpeed = 1f;
    [SerializeField] private LiquidType liquidType;

    //---Properties
    private float SurfaceHeight => transform.position.y + heightTiles * 0.5f - 0.1f;

    //---Private Variables
    private readonly Collider2D[] CollisionBuffer = new Collider2D[32];
    private int collisionCount;

    private readonly HashSet<Collider2D> splashedEntities = new();
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
        if (!splashExitPrefab) splashExitPrefab = splashPrefab;
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

        Color gray = new(0.5f, 0f, 0f, 1f);
        colors = new Color32[totalPoints];
        for (int i = 0; i < totalPoints; i++)
            colors[i] = gray;

        heightTex.Apply();

        hitbox.offset = new(0, heightTiles * 0.25f);
        hitbox.size = new(widthTiles * 0.5f, heightTiles * 0.5f);
        spriteRenderer.size = new(widthTiles * 0.5f, heightTiles * 0.5f + 0.5f);
        if (mask)
            mask.transform.localScale = new(widthTiles * mask.sprite.pixelsPerUnit / 32f, (heightTiles - 1f) * mask.sprite.pixelsPerUnit / 32f + 2f, 1f);

        properties = new();
        properties.SetTexture("Heightmap", heightTex);
        properties.SetFloat("WidthTiles", widthTiles);
        properties.SetFloat("Height", heightTiles);
        spriteRenderer.SetPropertyBlock(properties);
    }

    public void FixedUpdate() {
        // TODO: move to a compute shader?

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

    //public override void Render() {
    //    for (int i = 0; i < collisionCount; i++) {
    //        var obj = CollisionBuffer[i];

    //        if ()
    //    }
    //}

    public override void FixedUpdateNetwork() {
        // Find entities inside our collider
        collisionCount = Runner.GetPhysicsScene2D().OverlapBox((Vector2) transform.position + hitbox.offset, hitbox.size, 0, CollisionBuffer);

        for (int i = 0; i < collisionCount; i++) {
            var obj = CollisionBuffer[i];
            if (!obj || obj.GetComponentInParent<BasicEntity>() is not BasicEntity entity)
                continue;

            // Don't splash stationary stars
            if (entity is StarBouncer sb && sb.IsStationary)
                continue;

            // Don't splash stationary coins
            if (entity is FloatingCoin)
                continue;

            if (liquidType == LiquidType.Water && entity is PlayerController player && player.State == Enums.PowerupState.MiniMushroom) {
                if (!player.IsSwimming && Mathf.Abs(player.body.velocity.x) > 0.3f && player.body.velocity.y < 0) {
                    // player is running on the water
                    player.body.position = new(player.body.position.x, hitbox.bounds.max.y);
                    player.IsOnGround = true;
                    player.IsWaterWalking = true;
                    continue;
                }
            }

            if (Runner.IsServer) {
                if (!splashedEntities.Contains(obj)) {
                    bool splash = entity.body.position.y > SurfaceHeight - 0.5f && (entity is not PlayerController pl || pl.State != Enums.PowerupState.MiniMushroom || pl.body.velocity.y < -2f);
                    if (splash) {
                        Rpc_Splash(new(entity.body.position.x, SurfaceHeight), -Mathf.Abs(Mathf.Min(-5, entity.body.velocity.y)), ParticleType.Enter);
                    }
                    splashedEntities.Add(obj);
                }

                if (liquidType != LiquidType.Water && entity is not PlayerController) {
                    // Kill entity if they're below the surface of the posion/lava
                    bool underSurface = entity.GetComponentInChildren<Renderer>()?.bounds.max.y < SurfaceHeight;
                    if (underSurface) {
                        // Don't let fireballs "poof"
                        if (entity is FireballMover fm)
                            fm.PlayBreakEffect = false;

                        entity.Destroy(BasicEntity.DestroyCause.Lava);
                    }
                }
            }

            // Player collisions are special
            if (entity is PlayerController player2) {
                if (player2.IsDead || player2.CurrentPipe)
                    continue;

                if (liquidType == LiquidType.Water) {
                    float height = player2.body.position.y + (player2.WorldHitboxSize.y * 0.5f);
                    bool underwater = height <= SurfaceHeight;

                    if (player2.IsSwimming && !underwater && player2.body.velocity.y > 0) {
                        // jumped out of the water
                        player2.IsSwimming = false;
                        player2.SwimJump = true;
                        player2.SwimLeaveForceHoldJumpTime = Runner.SimulationTime + 0.3f;
                        if (Runner.IsServer)
                            Rpc_Splash(new(player2.body.position.x, SurfaceHeight), Mathf.Abs(Mathf.Max(5, player2.body.velocity.y)), ParticleType.Exit);
                    } else {
                        player2.IsSwimming = underwater;
                        player2.IsWaterWalking = false;
                    }
                } else {
                    player2.Death(false, liquidType == LiquidType.Lava);
                    continue;
                }
            }
        }

        foreach (var obj in splashedEntities) {
            if (CollisionBuffer.Contains(obj))
                continue;

            BasicEntity entity = obj.GetComponentInParent<BasicEntity>();

            if (entity is PlayerController player) {
                float height = player.body.position.y + player.WorldHitboxSize.y;
                bool underwater = height <= SurfaceHeight;

                if (underwater || player.body.velocity.y < 0) {
                    // swam out the side of the water
                    player.IsSwimming = true;
                } else {
                    // jumped out of the water
                    if (player.IsSwimming) {
                        player.IsSwimming = false;
                        player.SwimJump = true;
                        player.SwimLeaveForceHoldJumpTime = Runner.SimulationTime + 0.3f;
                        if (Runner.IsServer)
                            Rpc_Splash(new(player.body.position.x, SurfaceHeight), Mathf.Abs(Mathf.Max(5, player.body.velocity.y)), ParticleType.Exit);
                    }
                }
                continue;
            }

            if (Runner.IsServer) {
                bool aboveWater = (entity.body?.position.y ?? entity.transform.position.y) >= SurfaceHeight;
                if (aboveWater) {
                    Rpc_Splash(entity.body.position, Mathf.Abs(Mathf.Max(5, entity.body.velocity.y)), aboveWater ? ParticleType.Exit : ParticleType.None);
                }
            }
        }

        splashedEntities.IntersectWith(CollisionBuffer);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_Splash(Vector2 position, float power, ParticleType particle) {
        switch (particle) {
        case ParticleType.Enter:
            Instantiate(splashPrefab, position, Quaternion.identity);
            break;
        case ParticleType.Exit:
            Instantiate(splashExitPrefab, position, Quaternion.identity);
            break;
        }

        float tile = (transform.InverseTransformPoint(position).x / widthTiles + 0.25f) * 2f;
        int px = (int) (tile * totalPoints);
        for (int i = -splashWidth; i <= splashWidth; i++) {
            int pointsX = (px + totalPoints + i) % totalPoints;
            pointVelocities[pointsX] = -splashVelocity * power;
        }
    }

    //---Helpers
    public enum LiquidType {
        Water,
        Poison,
        Lava,
    }

    public enum ParticleType : byte {
        None,
        Enter,
        Exit,
    }
}
