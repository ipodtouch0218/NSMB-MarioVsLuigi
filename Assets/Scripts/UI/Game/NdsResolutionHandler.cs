using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.Game {
    public class NdsResolutionHandler : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private PlayerElements playerElements;
        [SerializeField] private GameObject ndsCanvas;
        [SerializeField] private RawImage ndsImage;
        [SerializeField] private AspectRatioFitter fitter;

        //---Private Variables
        private RenderTexture texture;

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
                return;
            }

            // NDS res enabled.
            bool resolutionChanged;
            if (Settings.Instance.GraphicsNdsForceAspect) {
                resolutionChanged = CreateRenderTexture(298, 224);
                fitter.enabled = true;
            } else {
                int width = Screen.currentResolution.width;
                int height = Screen.currentResolution.height;
                float aspect = (float) width / height;
                if (aspect > (4/3f)) {
                    // Width is larger than height
                    height = 224;
                    width = Mathf.CeilToInt(height * aspect);
                } else {
                    width = 298;
                    height = Mathf.CeilToInt(width / aspect);
                }
                resolutionChanged = CreateRenderTexture(width, height);

                if (fitter.enabled) {
                    RectTransform fitterTransform = fitter.GetComponent<RectTransform>();
                    fitter.enabled = false;
                    fitterTransform.anchorMin = Vector2.zero;
                    fitterTransform.anchorMax = Vector2.one;
                    fitterTransform.sizeDelta = Vector2.zero;
                }
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

            ndsCanvas.SetActive(true);
            return true;
        }

        private void ReleaseRenderTexture() {
            if (texture) {
                RenderTexture.ReleaseTemporary(texture);
            }
            texture = null;

            playerElements.Camera.targetTexture = null;
            if (playerElements.ScrollCamera) {
                playerElements.ScrollCamera.targetTexture = null;
            }
            ndsCanvas.SetActive(false);
        }

        private void OnNdsResolutionSettingChanged() {
            Update();
        }
    }
}
