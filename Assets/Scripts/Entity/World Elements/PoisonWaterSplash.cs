using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PoisonWaterSplash : MonoBehaviour {

    private int points = 256;
    public float tension = 40, kconstant = 2, damping = 0.95f, splashVelocity = 50f;
    private SpriteRenderer spriteRenderer;
    private Texture2D heightTex;
    private float[] pointHeights, pointVelocities;
    private Color32[] colors;
    void Start() {
        spriteRenderer = GetComponent<SpriteRenderer>();
        heightTex = new Texture2D(points, 1);
        pointHeights = new float[points];
        pointVelocities = new float[points];
        colors = new Color32[points];

        MaterialPropertyBlock properties = new MaterialPropertyBlock();
        properties.SetTexture("Heightmap", heightTex);
        spriteRenderer.SetPropertyBlock(properties);
    }
    void FixedUpdate() {

        for (int i = 0; i < points; i++) {
            float height = pointHeights[i];
            pointVelocities[i] += tension * (-height);
            pointVelocities[i] *= (damping);
        }
        for (int i = 0; i < points; i++) {
            pointHeights[i] += pointVelocities[i] * Time.fixedDeltaTime;
        }
        for (int i = 0; i < points; i++) {
            float height = pointHeights[i];

            pointVelocities[i] -= (kconstant * Time.fixedDeltaTime) * (height - pointHeights[(i+points-1)%points]); //left
            pointVelocities[i] -= (kconstant * Time.fixedDeltaTime) * (height - pointHeights[(i+points+1)%points]); //right
        }
        for (int i = 0; i < points; i++) {
            colors[i] = new Color((pointHeights[i]/20f) + 0.5f, 0, 0, 1);
        }

        heightTex.SetPixels32(colors, 0);
        heightTex.Apply();
    }
    void OnTriggerEnter2D(Collider2D collider) {
        GameObject.Instantiate(Resources.Load("Prefabs/Particle/PoisonWaterSplash"), collider.transform.position, Quaternion.identity);
        float x = collider.transform.position.x - transform.position.x;
        float xpoints = (x+points)%(points*transform.localScale.x);
        pointVelocities[(int) (xpoints/transform.localScale.x)] = -splashVelocity;
    }
}
