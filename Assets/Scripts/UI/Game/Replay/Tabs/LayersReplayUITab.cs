using NSMB.Utilities.Extensions;
using System;
using TMPro;
using UnityEngine;

namespace NSMB.UI.Game.Replay {
    public class LayersReplayUITab : ReplayUITab {

        //---Properties
        private Camera OurCamera => parent.playerElements.Camera;

        //---Serialized Variables
        [SerializeField] private ReplayRenderLayer[] layersPerButton;

        public override void OnEnable() {
            base.OnEnable();

            for (int i = 0; i < selectables.Count; i++) {
                ApplyColor(i);
            }
        }

        public void ToggleLayer(int index) {
            ReplayRenderLayer mask = layersPerButton[index];
            if (IsLayerMaskEnabled(mask.CameraLayerMask)) {
                DisableLayers(mask);
            } else {
                EnableLayers(mask);
            }
            GlobalController.Instance.sfx.PlayOneShot(SoundEffect.UI_Decide);
            ApplyColor(index);
        }

        public void ApplyColor(int index) {
            LayerMask mask = layersPerButton[index].CameraLayerMask;
            TMP_Text text = selectables[index].GetComponentInChildren<TMP_Text>();

            text.color = IsLayerMaskEnabled(mask) ? enabledColor : disabledColor;
        }

        public void EnableLayers(ReplayRenderLayer mask) {
            OurCamera.cullingMask = OurCamera.cullingMask | mask.CameraLayerMask;
            foreach (var b in mask.ObjectsToDisable) {
                b.enabled = true;
            }
        }

        public void DisableLayers(ReplayRenderLayer mask) {
            OurCamera.cullingMask = OurCamera.cullingMask & ~mask.CameraLayerMask;
            foreach (var b in mask.ObjectsToDisable) {
                b.enabled = false;
            }
        }

        private bool IsLayerMaskEnabled(LayerMask mask) {
            return (OurCamera.cullingMask & mask) != 0;
        }

        [Serializable]
        public class ReplayRenderLayer {
            public LayerMask CameraLayerMask;
            public Behaviour[] ObjectsToDisable;
        }
    }
}
