using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Animation))]
[ExecuteInEditMode]
public class LegacyAnimateSpriteRenderer : MonoBehaviour {

    public float frame;
    public Sprite[] frames;
    private new SpriteRenderer renderer;

    private void Start() {
        renderer = GetComponent<SpriteRenderer>();
    }

    private void Update() {
        int frame = Mathf.FloorToInt(this.frame);

        if (frame < 0 || frames == null) {
            this.frame = 0f;
            return;
        }

        if (frame >= frames.Length) {
            this.frame = frames.Length;
            return;
        }

        if (frames[frame] != renderer.sprite)
            renderer.sprite = frames[frame];
    }

    private void OnRenderObject() {
        if (!Application.isPlaying)
            Update();
    }
}
