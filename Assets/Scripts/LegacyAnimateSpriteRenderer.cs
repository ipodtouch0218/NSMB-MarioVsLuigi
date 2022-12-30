using UnityEngine;

[RequireComponent(typeof(SpriteRenderer), typeof(Animation))]
[ExecuteInEditMode]
public class LegacyAnimateSpriteRenderer : MonoBehaviour {

    //---Public Variables
    public float frame; //must be a float because animators dont support ints, apparently?

    //---Serialized Variables
    [SerializeField] private Sprite[] frames;

    //---Private Variables
    private SpriteRenderer sRenderer;

    public void Awake() {
        sRenderer = GetComponent<SpriteRenderer>();
    }

    public void Update() {
        int frame = Mathf.FloorToInt(this.frame);

        if (frames == null || frames.Length == 0)
            return;

        frame = Mathf.Clamp(frame, 0, frames.Length);

        if (frames[frame] != sRenderer.sprite)
            sRenderer.sprite = frames[frame];
    }

    public void OnRenderObject() {
        if (!Application.isPlaying)
            Update();
    }
}
