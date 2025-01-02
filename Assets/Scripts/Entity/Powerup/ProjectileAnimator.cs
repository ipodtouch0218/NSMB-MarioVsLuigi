using NSMB.Extensions;
using NSMB.UI.Game;
using Quantum;
using UnityEngine;
using UnityEngine.Rendering;

public class ProjectileAnimator : QuantumEntityViewComponent {

    //---Serialized Variables
    [SerializeField] private SpriteRenderer sRenderer;
    [SerializeField] private Color sameTeamColor, differentTeamColor;

    //---Private Variables
    private EntityRef owner;

    public void OnValidate() {
        this.SetIfNull(ref sRenderer, UnityExtensions.GetComponentType.Children);
    }

    public override unsafe void OnActivate(Frame f) {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        owner = f.Unsafe.GetPointer<Projectile>(EntityRef)->Owner;
    }

    public override void OnDeactivate() {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
    }

    private void OnBeginCameraRendering(ScriptableRenderContext src, Camera camera) {
        sRenderer.color = IsCameraTeamFocus(camera) ? sameTeamColor : differentTeamColor;
    }

    private unsafe bool IsCameraTeamFocus(Camera camera) {
        if (!PredictedFrame.Unsafe.TryGetPointer(owner, out MarioPlayer* ownerMario)) {
            return false;
        }

        foreach (var playerElement in PlayerElements.AllPlayerElements) {
            if (playerElement.Camera == camera || playerElement.ScrollCamera == camera) {
                // This camera.
                if (!PredictedFrame.Unsafe.TryGetPointer(playerElement.Entity, out MarioPlayer* cameraMario)) {
                    return false;
                }

                return cameraMario->GetTeam(PredictedFrame) == ownerMario->GetTeam(PredictedFrame);
            }
        }
        return false;
    }
}