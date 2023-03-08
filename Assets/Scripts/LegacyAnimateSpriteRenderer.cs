using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[ExecuteInEditMode]
public class LegacyAnimateSpriteRenderer : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private float frame; //must be a float because animators dont support ints, apparently?
    [SerializeField] private float fps = 8;
    [SerializeField] private Sprite[] frames;

    //---Private Variables
    private SpriteRenderer sRenderer;

    public void OnValidate() {
        frame = 0f;
    }

    public void Awake() {
        sRenderer = GetComponent<SpriteRenderer>();
        frame = 0f;
    }

    [ExecuteAlways]
    public void Update() {
        if (frames == null || frames.Length == 0 || !sRenderer)
            return;

        frame += fps * Time.deltaTime;
        frame = Mathf.Repeat(frame, frames.Length);
        int currentFrame = Mathf.FloorToInt(frame);

        if (frames[currentFrame] != sRenderer.sprite)
            sRenderer.sprite = frames[currentFrame];
    }
}
