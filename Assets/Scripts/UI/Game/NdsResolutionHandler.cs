using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.Game {
    public class NdsResolutionHandler : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private PlayerElements playerElements;
        [SerializeField] private Canvas rootCanvas;
        [SerializeField] private GameObject ndsBackground;
        [SerializeField] private RawImage ndsImage;
        [SerializeField] private AspectRatioFitter fitter;

        //---Private Variables
        private RenderTexture texture;
        private bool pixelPerfect;
        private (int, int) previousResolution;

        public void Start() {
            Settings.OnNdsResolutionSettingChanged += OnNdsResolutionSettingChanged;
        }

        public void OnDestroy() {
            Settings.OnNdsResolutionSettingChanged -= OnNdsResolutionSettingChanged;
            ReleaseRenderTexture();
        }

        public void Update() {
            if (!Settings.Instance.GraphicsNdsEnabled) {
                ReleaseRenderTexture();
                if (fitter.enabled) {
                    pixelPerfect = false;
                    fitter.enabled = false;
                }
                RectTransform fitterTransform = (RectTransform) fitter.transform;
                fitterTransform.anchorMin = Vector2.zero;
                fitterTransform.anchorMax = Vector2.one;
                fitterTransform.sizeDelta = Vector2.zero;
                return;
            }

            // NDS res enabled.
            int width = Screen.width;
            int height = Screen.height;
            float aspect = (float) width / height;
            bool resolutionChanged;
            if (Settings.Instance.GraphicsNdsForceAspect) {
                resolutionChanged = CreateRenderTexture(298, 224);
                RectTransform fitterTransform = (RectTransform) fitter.transform;

                if (Settings.Instance.GraphicsNdsPixelPerfect && (!pixelPerfect || resolutionChanged || previousResolution != (width, height))) {
                    // Enable pixel-perfect
                    fitter.enabled = false;
                    fitterTransform.anchorMax = fitterTransform.anchorMin = new Vector2(0.5f, 0.5f);
                    float scaling = Mathf.Min((float) width / texture.width, (float) height / texture.height);
                    if (scaling >= 1) {
                        scaling = Mathf.Floor(scaling);
                    } else {
                        scaling = 1 / Mathf.Ceil(1 / scaling);
                    }
                    scaling /= rootCanvas.scaleFactor;
                    fitterTransform.sizeDelta = new Vector2(texture.width * scaling, texture.height * scaling);
                    fitterTransform.hasChanged = true;
                    pixelPerfect = true;
                    previousResolution = (width, height);

                } else if (!Settings.Instance.GraphicsNdsPixelPerfect && (pixelPerfect || resolutionChanged)) {
                    // Disable pixel-perfect.
                    fitter.enabled = true;
                    fitterTransform.anchorMin = Vector2.zero;
                    fitterTransform.anchorMax = Vector2.one;
                    fitterTransform.sizeDelta = Vector2.zero;
                    fitterTransform.hasChanged = true;
                    pixelPerfect = false;
                }
            } else {
                if (aspect > (4/3f)) {
                    // Width is larger than height
                    height = 224;
                    width = Mathf.CeilToInt(height * aspect);
                } else {
                    width = 298;
                    height = Mathf.CeilToInt(width / aspect);
                }
                resolutionChanged = CreateRenderTexture(width, height);

                RectTransform fitterTransform = (RectTransform) fitter.transform;
                fitter.enabled = false;
                fitterTransform.hasChanged = resolutionChanged;
                fitterTransform.anchorMin = Vector2.zero;
                fitterTransform.anchorMax = Vector2.one;
                fitterTransform.sizeDelta = Vector2.zero;
                pixelPerfect = false;
            }

            if (resolutionChanged) {
                ndsImage.texture = texture;
            }
        }

        private bool CreateRenderTexture(int width, int height) {
            if (texture && texture.width == width && texture.height == height) {
                return false;
            }

            ReleaseRenderTexture();
            texture = RenderTexture.GetTemporary(width, height);
            texture.useMipMap = false;
            texture.filterMode = FilterMode.Point;

            playerElements.Camera.targetTexture = texture;
            if (playerElements.ScrollCamera) {
                playerElements.ScrollCamera.targetTexture = texture;
            }

            ndsBackground.SetActive(true);
            ndsImage.enabled = true;
            return true;
        }

        private void ReleaseRenderTexture() {
            if (texture) {
                RenderTexture.ReleaseTemporary(texture);
            }
            texture = null;

            if (playerElements.Camera) {
                playerElements.Camera.targetTexture = null;
            }
            if (playerElements.ScrollCamera) {
                playerElements.ScrollCamera.targetTexture = null;
            }
            ndsBackground.SetActive(false);
            ndsImage.enabled = false;
        }

        private void OnNdsResolutionSettingChanged() {
            Update();
        }
    }
}
