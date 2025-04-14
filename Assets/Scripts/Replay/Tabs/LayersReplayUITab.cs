using NSMB.Extensions;
using TMPro;
using UnityEngine;

public class LayersReplayUITab : ReplayUITab {

    //---Properties
    private Camera OurCamera => parent.playerElements.Camera;

    //---Serialized Variables
    [SerializeField] private LayerMask[] layersPerButton;

    public override void OnEnable() {
        base.OnEnable();

        for (int i = 0; i < selectables.Count; i++) {
            ApplyColor(i);
        }
    }

    public void ToggleLayer(int index) {
        LayerMask mask = layersPerButton[index];
        if (IsLayerMaskEnabled(mask)) {
            DisableLayers(mask);
        } else {
            EnableLayers(mask);
        }
        GlobalController.Instance.sfx.PlayOneShot(SoundEffect.UI_Decide);
        ApplyColor(index);
    }

    public void ApplyColor(int index) {
        LayerMask mask = layersPerButton[index];
        TMP_Text text = selectables[index].GetComponentInChildren<TMP_Text>();

        text.color = IsLayerMaskEnabled(mask) ? enabledColor : disabledColor;
    }

    public void EnableLayers(LayerMask mask) {
        OurCamera.cullingMask = OurCamera.cullingMask | mask;
    }

    public void DisableLayers(LayerMask mask) {
        OurCamera.cullingMask = OurCamera.cullingMask & ~mask;
    }
    private bool IsLayerMaskEnabled(LayerMask mask) {
        return (OurCamera.cullingMask & mask) != 0;
    }
}
