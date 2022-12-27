using System.Linq;
using UnityEngine;

using Fusion;

[RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
public class MarioBrosPlatform : NetworkBehaviour {

    //---Static Variables
    private static readonly Vector2 BumpOffset = new(-0.25f, -0.1f);
    private static readonly Color BlankColor = new(0, 0, 0, 255);

    //---Networked Variables
    [Networked, Capacity(8)] private NetworkLinkedList<BumpInfo> Bumps => default;

    //---Serialized Variables
    [Delayed] [SerializeField] private int platformWidth = 8, samplesPerTile = 8, bumpWidthPoints = 3, bumpBlurPoints = 6;
    [SerializeField] private float bumpDuration = 0.4f;
    [SerializeField] private bool changeCollider = true;

    //---Misc Variables
    private SpriteRenderer spriteRenderer;
    private MaterialPropertyBlock mpb;
    private Color32[] pixels;
    private Texture2D displacementMap;

    public void Awake() {
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
        spriteRenderer.size = new Vector2(platformWidth, 1.25f);

        if (changeCollider)
            GetComponent<BoxCollider2D>().size = new Vector2(platformWidth, 5f / 8f);

        displacementMap = new(platformWidth * samplesPerTile, 1);
        pixels = new Color32[platformWidth * samplesPerTile];

        mpb = new();
        spriteRenderer.GetPropertyBlock(mpb);

        mpb.SetFloat("PlatformWidth", platformWidth);
        mpb.SetFloat("PointsPerTile", samplesPerTile);
    }

    public override void Render() {
        for (int i = 0; i < platformWidth * samplesPerTile; i++)
            pixels[i] = BlankColor;

        foreach (BumpInfo bump in Bumps) {
            float percentageCompleted = bump.SpawnTime / bumpDuration;
            float v = Mathf.Sin(Mathf.PI * percentageCompleted);
            for (int x = -bumpWidthPoints - bumpBlurPoints; x <= bumpWidthPoints + bumpBlurPoints; x++) {
                int index = bump.point + x;
                if (index < 0 || index >= platformWidth * samplesPerTile)
                    continue;

                float color = v;
                if (x < -bumpWidthPoints || x > bumpWidthPoints) {
                    color *= Mathf.SmoothStep(1, 0, (float) (Mathf.Abs(x) - bumpWidthPoints) / bumpBlurPoints);
                }

                if ((pixels[index].r / 255f) >= color)
                    continue;

                pixels[index].r = (byte) (Mathf.Clamp01(color) * 255);
            }
        }
        displacementMap.SetPixels32(pixels);
        displacementMap.Apply();

        mpb.SetTexture("DisplacementMap", displacementMap);
        spriteRenderer.SetPropertyBlock(mpb);
    }

    public override void FixedUpdateNetwork() {
        //TODO: don't use linq
        foreach (BumpInfo bump in Bumps.ToList()) {
            if (bump.SpawnTime + bumpDuration > Runner.SimulationTime)
                Bumps.Remove(bump);
        }
    }

    public void Bump(PlayerController player, Vector2 worldPos) {

        float localPos = transform.InverseTransformPoint(worldPos).x;

        if (Mathf.Abs(localPos) > platformWidth) {
            worldPos.x += GameManager.Instance.LevelWidth;
            localPos = transform.InverseTransformPoint(worldPos).x;
        }

        localPos /= platformWidth + 1;
        localPos += 0.5f; // get rid of negative coords
        localPos *= samplesPerTile * platformWidth;

        if (displacementMap.GetPixel((int) localPos, 0).r != 0)
            return;

        player.PlaySound(Enums.Sounds.World_Block_Bump);
        InteractableTile.Bump(player, InteractableTile.InteractionDirection.Up, worldPos + BumpOffset);

        Bumps.Add(new BumpInfo() { point = (int) localPos, SpawnTime = Runner.SimulationTime });
    }

    //---Helpers
    private struct BumpInfo : INetworkStruct {
        public int point;
        [Networked] public float SpawnTime { get; set; }
    }
}
