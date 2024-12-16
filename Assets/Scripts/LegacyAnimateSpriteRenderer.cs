using NSMB.Extensions;
using UnityEngine;
using UnityEngine.UI;

[ExecuteInEditMode]
public class LegacyAnimateSpriteRenderer : MonoBehaviour {

    //---Public Variables
    public bool isDisplaying = true;

    //---Serialized Variables
    [SerializeField] private bool runInEditor = false;
    [SerializeField] private float frame; // Must be a float because legacy animators dont support ints, apparently?
    [SerializeField] private float fps = 8;
    [SerializeField] public Sprite[] frames;

    //---Components
    [SerializeField] private SpriteRenderer sRenderer;
    [SerializeField] private Image image;

    public void OnValidate() {
        this.SetIfNull(ref sRenderer);
        this.SetIfNull(ref image);

        ValidationUtility.SafeOnValidate(SetSprite);
    }

    [ExecuteAlways]
    public void LateUpdate() {
        if (!runInEditor && !Application.isPlaying)
            return;
        if (frames == null || frames.Length == 0 || (!sRenderer && !image) || !enabled)
            return;

        frame += fps * Time.deltaTime;
        SetSprite();
    }

    private void SetSprite() {
        if (!isDisplaying || frames == null || frames.Length == 0)
            return;

        frame = Mathf.Repeat(frame, frames.Length);
        int currentFrame = Mathf.FloorToInt(frame);
        Sprite currentSprite = frames[currentFrame];

        if (sRenderer && currentSprite != sRenderer.sprite) {
            sRenderer.sprite = currentSprite;
        }

        if (image && currentSprite != image.sprite) {
            image.sprite = currentSprite;
        }
    }
}
