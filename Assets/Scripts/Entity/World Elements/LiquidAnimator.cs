using Photon.Deterministic;
using Quantum;
using UnityEngine;

namespace NSMB.Entities.World {
    public unsafe class LiquidAnimator : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private GameObject splashPrefab, splashExitPrefab;
        [SerializeField] private SpriteMask mask;
        [SerializeField] private int pointsPerTile = 8, splashWidth = 2;
        [SerializeField] private float tension = 40, kconstant = 1.5f, damping = 0.92f, splashVelocity = 50f, animationSpeed = 1f;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private QuantumEntityView entity;

        //---Private Variables
        private Texture2D heightTex;
        private MaterialPropertyBlock properties;
        private Color32[] colors;
        private float[] pointHeights, pointVelocities;
        private float animTimer;
        private int totalPoints;
        private bool initialized;

        private float heightTiles;
        private int widthTiles;

        public void OnValidate() {
            ValidationUtility.SafeOnValidate(() => {
                if (!this) {
                    return;
                }
                QPrototypeLiquid liquid = GetComponent<QPrototypeLiquid>();
                if (liquid) {
                    Initialize(liquid.Prototype.WidthTiles, liquid.Prototype.HeightTiles);
                }
            });
        }

        public void Start() {
            QuantumEvent.Subscribe<EventLiquidSplashed>(this, OnLiquidSplashed);
        }

        public void Initialize(QuantumGame game) {
            var liquid = game.Frames.Predicted.Unsafe.GetPointer<Liquid>(entity.EntityRef);
            Initialize(liquid->WidthTiles, liquid->HeightTiles);
            initialized = true;
        }

        public void Initialize(int width, FP height) {
            widthTiles = width;
            heightTiles = height.AsFloat;

            totalPoints = widthTiles * pointsPerTile;
            pointHeights = new float[totalPoints];
            pointVelocities = new float[totalPoints];
            heightTex = new Texture2D(totalPoints, 1, TextureFormat.RGBA32, false);

            // TODO: eventually, change to a customrendertexture.
            // texture = new CustomRenderTexture(totalPoints, 1, RenderTextureFormat.RInt);

            Color32 gray = new Color(0.5f, 0.5f, 0.5f, 1);
            colors = new Color32[totalPoints];
            for (int i = 0; i < totalPoints; i++) {
                colors[i] = gray;
            }

            heightTex.SetPixels32(colors);
            heightTex.Apply();

            spriteRenderer.size = new(widthTiles * 0.5f, heightTiles * 0.5f + 0.5f);
            if (mask) {
                mask.transform.localScale = new(widthTiles * mask.sprite.pixelsPerUnit / 32f, (heightTiles - 1f) * mask.sprite.pixelsPerUnit / 32f + 2f, 1f);
            }

            properties = new();
            properties.SetTexture("Heightmap", heightTex);
            properties.SetFloat("WidthTiles", widthTiles);
            properties.SetFloat("Height", heightTiles);
            spriteRenderer.SetPropertyBlock(properties);
        }

        public void Update() {
            animTimer += animationSpeed * Time.deltaTime;
            animTimer %= 8;
        }

        public void FixedUpdate() {
            // TODO: move to a compute shader?
            if (!initialized) {
                return;
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

                pointVelocities[i] -= kconstant * delta * (height - pointHeights[(i + totalPoints - 1) % totalPoints]); // Left
                pointVelocities[i] -= kconstant * delta * (height - pointHeights[(i + totalPoints + 1) % totalPoints]); // Right
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

            properties.SetFloat("TextureIndex", animTimer);
            spriteRenderer.SetPropertyBlock(properties);
        }

        private void OnLiquidSplashed(EventLiquidSplashed e) {
            Instantiate(e.Exit ? splashExitPrefab : splashPrefab, e.Position.ToUnityVector3(), Quaternion.identity);

            float tile = (transform.InverseTransformPoint(e.Position.ToUnityVector3()).x / widthTiles + 0.25f) * 2f;
            int px = (int) (tile * totalPoints);
            for (int i = -splashWidth; i <= splashWidth; i++) {
                int pointsX = px + i;
                pointsX = (int) Mathf.Repeat(pointsX, totalPoints);

                pointVelocities[pointsX] = -splashVelocity * e.Force.AsFloat;
            }
        }



        /*


        private void Awake() {
            Initialize();
        }

        private void OnValidate() {
            ValidationUtility.SafeOnValidate(Initialize);
            if (!splashExitPrefab) {
                splashExitPrefab = splashPrefab;
            }
        }



        public override void FixedUpdateNetwork() {
            // Find entities inside our collider
            int collisionCount = Runner.GetPhysicsScene2D().OverlapBox((Vector2) transform.position + hitbox.offset, hitbox.size, 0, CollisionBuffer);

            for (int i = 0; i < collisionCount; i++) {
                var obj = CollisionBuffer[i];
                HandleEntityCollision(obj);
            }

            foreach (var obj in splashedEntities) {

                BasicEntity entity = obj.GetComponentInParent<BasicEntity>();
                if (!entity || !entity.Object || !entity.IsActive) {
                    continue;
                }

                float height = entity.body.Position.y + entity.Height * 0.5f;
                bool underwater = height <= SurfaceHeight;

                if (underwater || entity.body.Velocity.y < 0) {
                    // Swam out the side of the water
                    entity.InWater = this;

                } else if (entity.InWater) {
                    // Jumped out of the water
                    entity.InWater = null;
                    if (entity is PlayerController player) {
                        player.SwimJump = true;
                        player.SwimLeaveForceHoldJumpTime = Runner.SimulationTime + 0.3f;
                    }
                    if (Runner.IsServer) {
                        Rpc_Splash(new(entity.body.Position.x, SurfaceHeight), Mathf.Abs(Mathf.Max(5, entity.body.Velocity.y)), ParticleType.Exit);
                    }
                }
            }

            Utils.Utils.IntersectWithBuffer(splashedEntities, CollisionBuffer, collisionCount);
        }

        private void HandleEntityCollision(Collider2D obj) {
            if (!obj || obj.GetComponentInParent<BasicEntity>() is not BasicEntity entity) {
                return;
            }

            // Don't splash stationary stars
            if (entity is BigStar sb && sb.IsStationary) {
                return;
            }

            // Don't splash stationary coins
            if (entity is FloatingCoin) {
                return;
            }

            // Dont splash newly created powerups (bug fix)
            if (entity is Powerup powerup && powerup.SpawnAnimationTimer.IsActive(Runner)) {
                return;
            }

            PlayerController pl = entity as PlayerController;

            if (liquidType == LiquidType.Water && entity is PlayerController player && player.State == Enums.PowerupState.MiniMushroom) {
                if (!player.InWater && Mathf.Abs(player.body.Velocity.x) > 0.3f && player.body.Velocity.y < 0) {
                    // Player is running on the water
                    player.body.Position = new(player.body.Position.x, hitbox.bounds.max.y);
                    player.IsOnGround = true;
                    player.IsWaterWalking = true;
                    return;
                }
            }

            bool contains = splashedEntities.Contains(obj);
            if (Runner.IsServer && !contains) {
                bool splash = entity.body.Position.y > SurfaceHeight - 0.5f;
                if (pl) {
                    splash &= !pl.IsDead;
                    splash &= liquidType == LiquidType.Water;
                    splash &= pl.State != Enums.PowerupState.MiniMushroom || pl.body.Velocity.y < -2f;
                }

                if (splash && entity.IsActive && Mathf.Abs(entity.body.Velocity.y) > 1) {
                    Rpc_Splash(new(entity.body.Position.x, SurfaceHeight), -Mathf.Abs(Mathf.Min(-5, entity.body.Velocity.y)), ParticleType.Enter);
                }
            }

            if (!contains) {
                splashedEntities.Add(obj);
            }

            // Kill entity if they're below the surface of the posion/lava
            if (liquidType != LiquidType.Water && entity is not PlayerController) {
                bool underSurface = entity.GetComponentInChildren<Renderer>()?.bounds.max.y < SurfaceHeight;
                if (underSurface) {
                    // Don't let fireballs "poof"
                    if (entity is Fireball fm) {
                        if (fm.body.Position.y < SurfaceHeight - 0.2f) {
                            fm.DespawnEntity(false);
                        }
                    } else if (entity is not BigStar) {
                        if (entity is KillableEntity ke) {
                            ke.Crushed();
                        } else {
                            if (!entity.DespawnTimer.IsRunning) {
                                entity.DespawnTimer = TickTimer.CreateFromSeconds(Runner, 1f);
                            }
                            //entity.DespawnEntity();
                        }
                    }
                }
            }

            if (pl && (pl.IsDead || pl.CurrentPipe)) {
                return;
            }

            float height = entity.body.Position.y + (entity.Height * 0.5f);
            bool underwater = height <= SurfaceHeight;

            if (liquidType == LiquidType.Water) {
                if (entity.InWater && !underwater && entity.body.Velocity.y > 0) {
                    // Jumped out of the water
                    entity.InWater = null;
                    if (pl) {
                        pl.SwimJump = true;
                        pl.SwimLeaveForceHoldJumpTime = Runner.SimulationTime + 0.3f;
                    }
                    if (Runner.IsServer && Mathf.Abs(entity.body.Velocity.y) > 1) {
                        Rpc_Splash(new(entity.body.Position.x, SurfaceHeight), Mathf.Abs(Mathf.Max(5, entity.body.Velocity.y)), ParticleType.Exit);
                    }
                } else {
                    // Entered water
                    entity.InWater = underwater ? this : null;
                    if (pl) {
                        pl.IsWaterWalking = false;
                    }
                }
            } else {
                if (Runner.IsServer) {
                    Rpc_Splash(new(entity.body.Position.x, SurfaceHeight), -Mathf.Abs(-Mathf.Max(5, entity.body.Velocity.y)), ParticleType.Enter);
                }

                if (pl) {
                    pl.Death(false, liquidType == LiquidType.Lava);
                }
                return;
            }
        }


        //---Helpers
        public enum ParticleType : byte {
            None,
            Enter,
            Exit,
        }
        */
    }
}