using UnityEngine;

using Fusion;
using NSMB.Entities.Player;
using NSMB.Game;
using NSMB.Tiles;

namespace NSMB.Entities.World {

    [RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
    public class MarioBrosPlatform : NetworkBehaviour, IPlayerInteractable {

        //---Static Variables
        private static readonly Vector2 BumpOffset = new(-0.25f, -0.5f);
        private static readonly Color BlankColor = new(0, 0, 0, 255);
        private static readonly int ParamPlatformWidth = Shader.PropertyToID("PlatformWidth");
        private static readonly int ParamPointsPerTile = Shader.PropertyToID("PointsPerTile");
        private static readonly int ParamDisplacementMap = Shader.PropertyToID("DisplacementMap");

        //---Networked Variables
        [Networked, Capacity(10)] private NetworkLinkedList<BumpInfo> Bumps => default;

        //---Serialized Variables
        [SerializeField, Delayed] private int platformWidth = 8, samplesPerTile = 8, bumpWidthPoints = 3, bumpBlurPoints = 6;
        [SerializeField] private float bumpDuration = 0.4f;
        [SerializeField] private bool changeCollider = true;

        //---Misc Variables
        private Color32[] pixels;
        private SpriteRenderer spriteRenderer;
        private BoxCollider2D boxCollider;
        private MaterialPropertyBlock mpb;
        private Texture2D displacementMap;

        public void Awake() {
            Initialize();
        }

        public void OnValidate() {
            // Jank.
            ValidationUtility.SafeOnValidate(Initialize);
        }

        private void Initialize() {
            if (this == null) {
                // What
                return;
            }

            spriteRenderer = GetComponent<SpriteRenderer>();
            spriteRenderer.size = new Vector2(platformWidth, 1.25f);

            boxCollider = GetComponent<BoxCollider2D>();
            if (changeCollider) {
                boxCollider.size = new Vector2(platformWidth, 5f / 8f);
            }

            displacementMap = new(platformWidth * samplesPerTile, 1);
            pixels = new Color32[platformWidth * samplesPerTile];

            mpb = new();
            spriteRenderer.GetPropertyBlock(mpb);

            mpb.SetFloat(ParamPlatformWidth, platformWidth);
            mpb.SetFloat(ParamPointsPerTile, samplesPerTile);
        }

        public override void Render() {
            for (int i = 0; i < platformWidth * samplesPerTile; i++) {
                pixels[i] = BlankColor;
            }

            foreach (BumpInfo bump in Bumps) {
                float percentageCompleted = (Runner.LocalRenderTime - (bump.SpawnTick * Runner.DeltaTime)) / bumpDuration;
                float v = Mathf.Sin(Mathf.PI * percentageCompleted);

                for (int x = -bumpWidthPoints - bumpBlurPoints; x <= bumpWidthPoints + bumpBlurPoints; x++) {
                    int index = bump.Point + x;
                    if (index < 0 || index >= platformWidth * samplesPerTile) {
                        continue;
                    }

                    float color = v;
                    if (x < -bumpWidthPoints || x > bumpWidthPoints) {
                        color *= Mathf.SmoothStep(1, 0, (float) (Mathf.Abs(x) - bumpWidthPoints) / bumpBlurPoints);
                    }

                    if ((pixels[index].r / 255f) >= color) {
                        continue;
                    }

                    pixels[index].r = (byte) (Mathf.Clamp01(color) * 255);
                }
            }

            displacementMap.SetPixels32(pixels);
            displacementMap.Apply();

            mpb.SetTexture(ParamDisplacementMap, displacementMap);
            spriteRenderer.SetPropertyBlock(mpb);
        }

        public override void FixedUpdateNetwork() {
            for (int i = Bumps.Count - 1; i >= 0; i--) {
                BumpInfo bump = Bumps[i];
                if (bump.SpawnTick + (bumpDuration / Runner.DeltaTime) < Runner.Tick) {
                    Bumps.Remove(bump);
                }
            }
        }

        [Rpc]
        public void Rpc_Bump(PlayerController player, Vector2 worldPos) {

            float localPos = transform.InverseTransformPoint(worldPos).x;

            if (Mathf.Abs(localPos) > platformWidth) {
                worldPos.x += GameManager.Instance.LevelWidth;
                localPos = transform.InverseTransformPoint(worldPos).x;
            }

            localPos /= platformWidth + 1;
            localPos += 0.5f; // Get rid of negative coords
            localPos = Mathf.Clamp01(localPos);
            localPos *= samplesPerTile * platformWidth;

            foreach (BumpInfo bump in Bumps) {
                // If we're too close to another bump, don't create a new one.
                if (Mathf.Abs(bump.Point - localPos) < bumpWidthPoints + bumpBlurPoints) {
                    return;
                }
            }

            InteractableTile.Bump(player, InteractionDirection.Up, worldPos + BumpOffset);

            Bumps.Add(new BumpInfo() { Point = (int) localPos, SpawnTick = Runner.Tick });
        }

        //---IPlayerInteractable overrides
        public void InteractWithPlayer(PlayerController player, PhysicsDataStruct.IContactStruct contact = null) {
            if (player.IsInKnockback || player.IsFrozen || player.body.Position.y > transform.position.y) {
                return;
            }

            if (contact == null || contact.direction != InteractionDirection.Up) {
                return;
            }

            Rpc_Bump(player, new(player.body.Position.x, transform.position.y));
        }

        //---Helpers
        private struct BumpInfo : INetworkStruct {
            public int Point;
            public int SpawnTick;
        }
    }
}
