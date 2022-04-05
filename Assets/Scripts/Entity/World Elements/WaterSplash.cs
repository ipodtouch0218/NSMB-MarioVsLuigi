using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider2D))]
public class WaterSplash : MonoBehaviour {

    [Delayed]
    public int widthTiles = 64, pointsPerTile = 4;
    [Delayed]
    public float height = 0;
    public float tension = 40, kconstant = 2, damping = 0.95f, splashVelocity = 50f, resistance = 0f;
    public string splashParticle;
    private Texture2D heightTex;
    private float[] pointHeights, pointVelocities;
    private Color32[] colors;
    public AudioSource sfx;

    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;
    private Mesh mesh;

    void Start() {
        OnValidate();
    }
    private void OnValidate() {
        meshRenderer = GetComponent<MeshRenderer>();
        meshFilter = GetComponent<MeshFilter>();
        BoxCollider2D collider = GetComponent<BoxCollider2D>();

        CreateMesh();

        int points = widthTiles * pointsPerTile;
        heightTex = new Texture2D(points, 1);
        pointHeights = new float[points];
        pointVelocities = new float[points];
        colors = new Color32[points];

        for (int i = 0; i < points; i++)
            colors[i] = new(128, 128, 128, 255);
        heightTex.SetPixels32(colors);
        heightTex.Apply();

        collider.size = new(widthTiles / 2f, height + 0.5f);
        collider.offset = new(0, height / 2f);

        MaterialPropertyBlock properties = new();
        properties.SetTexture("Heightmap", heightTex);
        properties.SetFloat("Points", widthTiles * pointsPerTile);
        properties.SetFloat("PointsPerTile", pointsPerTile);
        properties.SetFloat("Height", height);
        meshRenderer.SetPropertyBlock(properties);
        meshRenderer.localBounds = new Bounds(collider.offset, collider.size);
    }
    void FixedUpdate() {
        int points = widthTiles * pointsPerTile;
        for (int i = 0; i < points; i++) {
            float height = pointHeights[i];
            pointVelocities[i] += tension * -height;
            pointVelocities[i] *= damping;
        }
        for (int i = 0; i < points; i++) {
            pointHeights[i] += pointVelocities[i] * Time.fixedDeltaTime;
        }
        for (int i = 0; i < points; i++) {
            float height = pointHeights[i];

            pointVelocities[i] -= kconstant * Time.fixedDeltaTime * (height - pointHeights[(i + points - 1) % points]); //left
            pointVelocities[i] -= kconstant * Time.fixedDeltaTime * (height - pointHeights[(i + points + 1) % points]); //right
        }
        for (int i = 0; i < points; i++) {
            colors[i] = new Color((pointHeights[i]/20f) + 0.5f, 0, 0, 1);
        }

        heightTex.SetPixels32(colors, 0);
        heightTex.Apply();

        Debug.Log(heightTex.GetPixel(128, 0));
    }
    void OnTriggerEnter2D(Collider2D collider) {
        int points = widthTiles * pointsPerTile;
        Instantiate(Resources.Load(splashParticle), collider.transform.position, Quaternion.identity);
        if (sfx)
            sfx.Play();
        float localX = transform.InverseTransformPoint(collider.transform.position).x;
        float pointsX = localX  % points;
        while (pointsX < 0)
            pointsX += points;

        Rigidbody2D body = collider.attachedRigidbody;
        float power = body ? -body.velocity.y : 1;
        pointVelocities[(int) pointsX] = -splashVelocity * power;
    }
    void OnTriggerStay2D(Collider2D collision) {
        if (collision.attachedRigidbody == null)
            return;

        collision.attachedRigidbody.velocity *= 1-Mathf.Clamp01(resistance);
    }
    private void CreateMesh() {
        //create verts
        int segmentCount = widthTiles * pointsPerTile;
        Vector3[] verts = new Vector3[(segmentCount + 1) * 2];
        Vector2[] uvs = new Vector2[verts.Length];
        float distancePerPoint = .5f / pointsPerTile;
        float xOffset = segmentCount / pointsPerTile / 4f;
        for (int x = 0; x <= segmentCount; x++) {
            int i = x * 2;
            float xPos = x * distancePerPoint;
            verts[i] = new(xPos - xOffset, -5f / 16f);
            verts[i + 1] = new(xPos - xOffset, 5f / 16f);

            uvs[i] = new((float) x / (segmentCount + 1), 0f);
            uvs[i + 1] = new((float) (x + 1) / (segmentCount + 1), 1f);
        }

        //create mesh
        mesh = new();
        mesh.name = "i hate making my own meshes";
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
}
