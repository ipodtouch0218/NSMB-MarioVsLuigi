using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider2D))]
public class WaterSplash : MonoBehaviour {

    [Delayed]
    public int widthTiles = 64, pointsPerTile = 4, splashWidth = 1;
    [Delayed]
    public float height = 1;
    public float tension = 40, kconstant = 2, damping = 0.95f, splashVelocity = 50f, resistance = 0f;
    public string splashParticle;
    private Texture2D heightTex;
    private float[] pointHeights, pointVelocities;
    private Color32[] colors;

    private int totalPoints;
    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;
    private Mesh mesh;
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

        meshRenderer = GetComponent<MeshRenderer>();
        meshFilter = GetComponent<MeshFilter>();
        BoxCollider2D collider = GetComponent<BoxCollider2D>();

        CreateMesh();

        totalPoints = widthTiles * pointsPerTile;
        pointHeights = new float[totalPoints];
        pointVelocities = new float[totalPoints];
        colors = new Color32[totalPoints];

        heightTex = new Texture2D(totalPoints, 1);
        for (int i = 0; i < totalPoints; i++)
            colors[i] = new Color(.5f, 0, 0, 1);
        heightTex.SetPixels32(colors);
        heightTex.Apply();

        collider.size = new(widthTiles / 2f, height + 0.5f);
        collider.offset = new(0, height / 2f);

        properties = new();
        properties.SetTexture("Heightmap", heightTex);
        properties.SetFloat("Points", totalPoints);
        properties.SetFloat("PointsPerTile", pointsPerTile);
        properties.SetFloat("Height", height);
        meshRenderer.SetPropertyBlock(properties);
        meshRenderer.localBounds = new Bounds(collider.offset, collider.size + Vector2.up);
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
            colors[i].r = (byte) ((Mathf.Clamp01(pointHeights[i] / 20f) + 0.5f) * 255);
        }

        heightTex.SetPixels32(colors, 0);
        heightTex.Apply();
    }
    void OnTriggerEnter2D(Collider2D collider) {
        Instantiate(Resources.Load(splashParticle), collider.transform.position, Quaternion.identity);

        Rigidbody2D body = collider.attachedRigidbody;
        float power = body ? -body.velocity.y : 1;
        float tile = transform.InverseTransformPoint(collider.transform.position).x * 2f;
        for (int i = -splashWidth; i <= splashWidth; i++) {
            int pointsX = (int) ((tile * pointsPerTile + i + totalPoints) % totalPoints);
            pointVelocities[pointsX] = -splashVelocity * power;
        }
    }
    void OnTriggerStay2D(Collider2D collision) {
        if (collision.attachedRigidbody == null)
            return;

        collision.attachedRigidbody.velocity *= 1-Mathf.Clamp01(resistance);
    }
    private void CreateMesh() {
        //create verts
        Vector3[] verts = new Vector3[(totalPoints + 1) * 2];
        Vector2[] uvs = new Vector2[verts.Length];
        float distancePerPoint = .5f / pointsPerTile;
        float xOffset = totalPoints / pointsPerTile / 4f;
        for (int x = 0; x <= totalPoints; x++) {
            int i = x * 2;
            float xPos = x * distancePerPoint;
            verts[i] = new(xPos - xOffset, -5f / 16f);
            verts[i + 1] = new(xPos - xOffset, 5f / 16f);

            uvs[i] = new((float) x / (totalPoints + 1), 0f);
            uvs[i + 1] = new((float) (x + 1) / (totalPoints + 1), 1f);
        }

        //create mesh
        mesh = new();
        mesh.name = "i hate making my own meshes";
        int[] tris = new int[totalPoints * 6];
        for (int i = 0; i < totalPoints; i++) {
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
