using Photon.Deterministic;
using Quantum;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;

namespace NSMB.Entities.World {
    public unsafe class LiquidAnimator : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private GameObject splashPrefab, lightSplashPrefab, splashExitPrefab;
        [SerializeField] private SpriteMask mask;
        [SerializeField] private int pointsPerTile = 8, splashWidth = 2;
        [SerializeField] private float tension = 40, kconstant = 1.5f, damping = 0.92f, splashVelocity = 50f, animationSpeed = 1f, minimumSplashStrength = 2f;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private QuantumEntityView entity;
        [SerializeField] private ParticleSystem bubbles;

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
            if (!entity) {
                Initialize(Mathf.RoundToInt(spriteRenderer.size.x * 2), Mathf.RoundToInt(spriteRenderer.size.y * 2) - 1);
            } else {
                QuantumEvent.Subscribe<EventLiquidSplashed>(this, OnLiquidSplashed, FilterOutReplayFastForward, onlyIfActiveAndEnabled: true, onlyIfEntityViewBound: true);
            }
        }

        public void Initialize(QuantumGame game) {
            var liquid = game.Frames.Predicted.Unsafe.GetPointer<Liquid>(entity.EntityRef);
            Initialize(liquid->WidthTiles, liquid->HeightTiles);
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

            spriteRenderer.size = new(widthTiles * 0.5f, heightTiles * 0.5f + 0.6f);
            if (mask) {
                mask.transform.localScale = new(widthTiles * mask.sprite.pixelsPerUnit / 32f, (heightTiles - 0.5f) * mask.sprite.pixelsPerUnit / 32f, 1f);
            }

            properties = new();
            properties.SetTexture("Heightmap", heightTex);
            properties.SetFloat("WidthTiles", widthTiles);
            properties.SetFloat("Height", heightTiles);
            spriteRenderer.SetPropertyBlock(properties);

            if (bubbles) {
                var emission = bubbles.emission;
                emission.rateOverTime = widthTiles / 4;

                var shape = bubbles.shape;
                shape.radius = widthTiles / 4;
            }

            initialized = true;
        }

        public void Update() {
            if (!initialized) {
                return;
            }

            animTimer += animationSpeed * Time.deltaTime;
            animTimer %= 8;

            properties.SetFloat("TextureIndex", animTimer);
            spriteRenderer.SetPropertyBlock(properties);
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
        }

        private void OnLiquidSplashed(EventLiquidSplashed e) {
            if (e.Entity != entity.EntityRef) {
                return;
            }

            Frame f = e.Game.Frames.Predicted;
            bool light = false;
            if (lightSplashPrefab
                && f.Unsafe.TryGetPointer(e.Splasher, out MarioPlayer* mario)
                && mario->CurrentPowerupState == PowerupState.MiniMushroom && !mario->IsGroundpounding
                && !mario->IsDead) {
                // Mini mario splashed
                //prefab = lightSplashPrefab;
                light = true;
            }

            GameObject prefab = e.Exit ? splashExitPrefab : splashPrefab;
            if (!e.Exit && light) {
                prefab = lightSplashPrefab;
            }

            GameObject particle = Instantiate(prefab, e.Position.ToUnityVector3(), Quaternion.identity);
            if (e.Exit && light) {
                particle.GetComponent<AudioSource>().volume = 0;
            }

            float tile = (transform.InverseTransformPoint(e.Position.ToUnityVector3()).x / widthTiles + 0.25f) * 2f;
            int px = (int) (tile * totalPoints);
            for (int i = -splashWidth; i <= splashWidth; i++) {
                int pointsX = px + i;
                pointsX = (int) Mathf.Repeat(pointsX, totalPoints);

                pointVelocities[pointsX] = -splashVelocity * Mathf.Max(minimumSplashStrength, e.Force.AsFloat);
            }
        }
    }
}