using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[ExecuteInEditMode]
public class LegacyAnimateSpriteRenderer : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private bool runInEditor = false;
    [SerializeField] private float frame; // Must be a float because legacy animators dont support ints, apparently?
    [SerializeField] private float fps = 8;
    [SerializeField] private Sprite[] frames;

    //---Components
    [SerializeField] private SpriteRenderer sRenderer;

    public void OnValidate() {
        if (!sRenderer) sRenderer = GetComponent<SpriteRenderer>();

        ValidationUtility.SafeOnValidate(SetSprite);
    }

    [ExecuteAlways]
    public void Update() {
#if UNITY_EDITOR
        if (!runInEditor && !Application.isPlaying)
            return;
#endif
        if (frames == null || frames.Length == 0 || !sRenderer || !enabled)
            return;

        frame += fps * Time.deltaTime;
        SetSprite();
    }

    private void SetSprite() {
        if (!sRenderer)
            return;

        frame = Mathf.Repeat(frame, frames.Length);
        int currentFrame = Mathf.FloorToInt(frame);

        if (frames[currentFrame] != sRenderer.sprite)
            sRenderer.sprite = frames[currentFrame];
    }
}
