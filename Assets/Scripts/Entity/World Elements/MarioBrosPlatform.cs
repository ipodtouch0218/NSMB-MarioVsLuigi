using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
public class MarioBrosPlatform : MonoBehaviour {

    [Delayed]
    public int platformWidth = 8, samplesPerTile = 8, bumpWidthPoints = 3, bumpBlurPoints = 6;
    public float bumpDuration = 0.4f;
    public bool changeCollider = true;

    private readonly List<BumpInfo> bumps = new();
    private MaterialPropertyBlock mpb;
    private Texture2D displacementMap;
    private SpriteRenderer spriteRenderer;
    private Color32[] pixels;

    public void Awake() {
        Initialize();
    }

    public void OnValidate() {
        Initialize();
    }

    private void Initialize() {
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
    /*
    private void CreateMesh() {
        //create verts
        int segmentCount = platformWidth * pointsPerTile;
        Vector3[] verts = new Vector3[(segmentCount + 1) * 2];
        Vector2[] uvs = new Vector2[verts.Length];
        float distancePerPoint = 1f / pointsPerTile;
        float xOffset = segmentCount / pointsPerTile / 2f;
        for (int x = 0; x <= segmentCount; x++) {
            int i = x * 2;
            float xPos = x * distancePerPoint;
            verts[i] = new(xPos - xOffset, -5f / 16f);
            verts[i + 1] = new(xPos - xOffset, 5f / 16f);

            if (x < pointsPerTile) {
                uvs[i] = new(0f, 0f);
                uvs[i + 1] = new(.5f, 1f);
            } else if (x > segmentCount - pointsPerTile) {
                uvs[i] = new(0f, 0f);
                uvs[i + 1] = new(, 1f);
            } else {
                uvs[i] = new(.5f, 0f);
                uvs[i + 1] = new(1f, 1f);
            }
        }

        //create mesh
        mesh = new();
        mesh.name = "Mario Bros Platform Mesh";
        int[] tris = new int[segmentCount * 6];
        for (int i = 0; i < segmentCount; i++) {
            int v = i * 6;
            int p = i * 2;

            tris[v + 0] = p + 0;
            tris[v + 4] = tris[v + 1] = p + 1;
            tris[v + 3] = tris[v + 2] = p + 2;
            tris[v + 5] = p + 3;
        }

        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.uv = uvs;
        meshFilter.mesh = mesh;
    }
    */
    public void Update() {
        for (int i = 0; i < platformWidth * samplesPerTile; i++)
            pixels[i] = new Color32(0, 0, 0, 255);

        for (int i = bumps.Count - 1; i >= 0; i--) {
            BumpInfo bump = bumps[i];
            bump.timer -= Time.deltaTime;
            if (bump.timer <= 0) {
                bumps.RemoveAt(i);
                continue;
            }
            
            float percentageCompleted = bump.timer / bumpDuration;
            float v = Mathf.Sin(Mathf.PI * percentageCompleted);
            for (int x = -bumpWidthPoints - bumpBlurPoints; x <= bumpWidthPoints + bumpBlurPoints; x++) {
                int index = bump.pointX + x;
                if (index < 0 || index >= platformWidth * samplesPerTile)
                    continue;

                float color = v;
                if (x < -bumpWidthPoints || x > bumpWidthPoints) {
                    color *= Mathf.SmoothStep(1, 0, (float) (Mathf.Abs(x) - bumpWidthPoints) / bumpBlurPoints);
                }

                if ((pixels[index].r / 255f) >= color)
                    continue;

                pixels[index] = new Color32((byte) (color * 255), 0, 0, 255);
            }
        }
        displacementMap.SetPixels32(pixels);
        displacementMap.Apply();

        mpb.SetTexture("DisplacementMap", displacementMap);
        spriteRenderer.SetPropertyBlock(mpb);
    }

    public void Bump(PlayerController player, Vector3 worldPos) {
        float localPos = transform.InverseTransformPoint(worldPos).x;
        InteractableTile.Bump(player, InteractableTile.InteractionDirection.Up, worldPos);

        if (Mathf.Abs(localPos) > platformWidth) {
            worldPos.x += GameManager.Instance.levelWidthTile / 2f;
            localPos = transform.InverseTransformPoint(worldPos).x;
        }

        localPos /= platformWidth + 1;
        localPos += 0.5f; // get rid of negative coords
        localPos *= samplesPerTile * platformWidth;

        bumps.Add(new BumpInfo(bumpDuration, (int) localPos));
    }

    class BumpInfo {
        public float timer;
        public int pointX;
        public BumpInfo(float timer, int pointX) {
            this.timer = timer;
            this.pointX = pointX;
        }
    }
}
