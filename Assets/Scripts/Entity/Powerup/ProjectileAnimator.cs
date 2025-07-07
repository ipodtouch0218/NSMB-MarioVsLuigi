using NSMB.UI.Game;
using NSMB.Utilities.Components;
using NSMB.Utilities.Extensions;
using Quantum;
using UnityEngine;
using UnityEngine.Rendering;

namespace NSMB.Entities.Player {
    public class ProjectileAnimator : QuantumEntityViewComponent {

        //---Serialized Variables
        [SerializeField] private SpriteRenderer sRenderer;
        [SerializeField] private Animator animator;
        [SerializeField] private LegacyAnimateSpriteRenderer legacySpriteAnimator;
        [SerializeField] private Color sameTeamColor, differentTeamColor;

        //---Private Variables
        private EntityRef owner;

        public void OnValidate() {
            this.SetIfNull(ref sRenderer, UnityExtensions.GetComponentType.Children);
            this.SetIfNull(ref animator, UnityExtensions.GetComponentType.Children);
            this.SetIfNull(ref legacySpriteAnimator, UnityExtensions.GetComponentType.Children);
        }

        public override unsafe void OnActivate(Frame f) {
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            var projectile = f.Unsafe.GetPointer<Projectile>(EntityRef);

            owner = projectile->Owner;

            if (projectile->FacingRight) {
                if (sRenderer) {
                    sRenderer.flipX = true;
                }
                if (animator) {
                    animator.Play("Left");
                }
            }
        }

        public override unsafe void OnUpdateView() {
            if (animator) {
                animator.enabled = PredictedFrame.Global->GameState == GameState.Playing;
            }
            if (legacySpriteAnimator) {
                legacySpriteAnimator.enabled = PredictedFrame.Global->GameState == GameState.Playing;
            }
        }

        public override void OnDeactivate() {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        }

        private void OnBeginCameraRendering(ScriptableRenderContext src, Camera camera) {
            /* Try/Catch is a bodge for this error:
                Render Pipeline error : the XR layout still contains active passes. Executing XRSystem.EndLayout() right now.
                NullReferenceException
                  at (wrapper managed-to-native) UnityEngine.SpriteRenderer.set_color_Injected(UnityEngine.SpriteRenderer,UnityEngine.Color&)
                  at UnityEngine.SpriteRenderer.set_color (UnityEngine.Color value) [0x00000] in <935634f5cc14479dbaa30641d55600a9>:0 
                  at ProjectileAnimator.OnBeginCameraRendering (UnityEngine.Rendering.ScriptableRenderContext src, UnityEngine.Camera camera) [0x0000d] in <e9b2d65d314645db895f8bc71e0abf60>:0 
                  at UnityEngine.Rendering.RenderPipelineManager.BeginCameraRendering (UnityEngine.Rendering.ScriptableRenderContext context, UnityEngine.Camera camera) [0x0000a] in <935634f5cc14479dbaa30641d55600a9>:0 
                  at UnityEngine.Rendering.RenderPipeline.BeginCameraRendering (UnityEngine.Rendering.ScriptableRenderContext context, UnityEngine.Camera camera) [0x00001] in <935634f5cc14479dbaa30641d55600a9>:0 
                  at UnityEngine.Rendering.Universal.UniversalRenderPipeline.RenderCameraStack (UnityEngine.Rendering.ScriptableRenderContext context, UnityEngine.Camera baseCamera) [0x002ba] in <26b2602f421d48c299968e0ff9498adf>:0 
                  at UnityEngine.Rendering.Universal.UniversalRenderPipeline.Render (UnityEngine.Rendering.ScriptableRenderContext renderContext, System.Collections.Generic.List`1[T] cameras) [0x0009b] in <26b2602f421d48c299968e0ff9498adf>:0 
                  at UnityEngine.Rendering.RenderPipeline.InternalRender (UnityEngine.Rendering.ScriptableRenderContext context, System.Collections.Generic.List`1[T] cameras) [0x0001c] in <935634f5cc14479dbaa30641d55600a9>:0 
                  at UnityEngine.Rendering.RenderPipelineManager.DoRenderLoop_Internal (UnityEngine.Rendering.RenderPipelineAsset pipe, System.IntPtr loopPtr, UnityEngine.Object renderRequest) [0x00046] in <935634f5cc14479dbaa30641d55600a9>:0 
            */
            try {
                if (sRenderer) {
                    sRenderer.color = IsCameraTeamFocus(camera) ? sameTeamColor : differentTeamColor;
                }
            } catch {
                // Debug.LogWarning("The bug happened");
            }
        }

        private unsafe bool IsCameraTeamFocus(Camera camera) {
            if (!PredictedFrame.Unsafe.TryGetPointer(owner, out MarioPlayer* ownerMario)) {
                return false;
            }

            foreach (var playerElement in PlayerElements.AllPlayerElements) {
                if (camera == playerElement.Camera || camera == playerElement.ScrollCamera || camera == playerElement.UICamera) {
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
}
