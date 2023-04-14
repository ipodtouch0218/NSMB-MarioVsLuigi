using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[ExecuteInEditMode]
public class LegacyAnimateSpriteRenderer : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private float frame; // Must be a float because legacy animators dont support ints, apparently?
    [SerializeField] private float fps = 8;
    [SerializeField] private Sprite[] frames;

    //---Private Variables
    private SpriteRenderer sRenderer;

    public void Awake() {
        sRenderer = GetComponent<SpriteRenderer>();
    }

    [ExecuteAlways]
    public void Update() {
        if (frames == null || frames.Length == 0 || !sRenderer || !enabled)
            return;

        frame += fps * Time.deltaTime;
        frame = Mathf.Repeat(frame, frames.Length);
        int currentFrame = Mathf.FloorToInt(frame);

        if (frames[currentFrame] != sRenderer.sprite)
            sRenderer.sprite = frames[currentFrame];
    }
}
