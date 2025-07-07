using NSMB.Utilities.Extensions;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.Utilities.Components {
    [ExecuteInEditMode]
    public class LegacyAnimateSpriteRenderer : MonoBehaviour {

        //---Public Variables
        public bool isDisplaying = true;
        public float frame; // Must be a float because legacy animators dont support ints, apparently?

        //---Properties
        public Sprite[] ActiveFrames => (Settings.Instance && Settings.Instance.GraphicsColorblind && colorblindModeFrames != null && colorblindModeFrames.Length != 0) ? colorblindModeFrames : frames;

        //---Serialized Variables
        [SerializeField] private bool runInEditor = false;
        [SerializeField] private float fps = 8;
        [SerializeField] public Sprite[] frames, colorblindModeFrames;
        [SerializeField] private bool useUnscaledDelta = false;

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
            if (!runInEditor && !Application.isPlaying) {
                return;
            }

            Sprite[] activeFrames = ActiveFrames;

            if (activeFrames == null || activeFrames.Length == 0 || (!sRenderer && !image) || !enabled) {
                return;
            }

            frame += fps * (useUnscaledDelta ? Time.unscaledDeltaTime : Time.deltaTime);
            SetSprite();
        }

        private void SetSprite() {
            Sprite[] activeFrames = ActiveFrames;
            if (!isDisplaying || activeFrames == null || activeFrames.Length == 0) {
                return;
            }

            frame = Mathf.Repeat(frame, activeFrames.Length);
            int currentFrame = Mathf.FloorToInt(frame);
            Sprite currentSprite = activeFrames[currentFrame];

            if (sRenderer /*&& currentSprite != sRenderer.sprite*/) {
                sRenderer.sprite = currentSprite;
            }

            if (image /*&& currentSprite != image.sprite*/) {
                image.sprite = currentSprite;
            }
        }
    }
}
