using NSMB.Entities.Player;
using NSMB.UI.Game;
using NSMB.Utilities.Components;
using NSMB.Utilities.Extensions;
using Quantum;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NSMB.Entities.CoinItems {
    public class ProjectileAnimator : QuantumEntityViewComponent {

        //---Serialized Variables
        [Header("Sprite")]
        [SerializeField] private SpriteRenderer sRenderer;
        [SerializeField] private LegacyAnimateSpriteRenderer legacySpriteAnimator;
        [SerializeField] private Color sameTeamColor, differentTeamColor;

        [Header("Model")]
        [SerializeField] private Animator animator;
        [SerializeField] private Color sameTeamColor3D = Color.white, differentTeamColor3D = Color.white;
        [SerializeField] private PowerupVisuals.MaterialTextureReplacement[] textureReplacements;

        //---Static Variables
        #region Shader Properties
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");
        private static readonly int OverallsMask = Shader.PropertyToID("_OverallsMask");
        private static readonly int ShirtMask = Shader.PropertyToID("_ShirtMask");
        private static readonly int CapMask = Shader.PropertyToID("_CapMask");
        private static readonly int OverallsColor = Shader.PropertyToID("_OverallsColor");
        private static readonly int ShirtColor = Shader.PropertyToID("_ShirtColor");
        private static readonly int CapUsesOverallsColor = Shader.PropertyToID("_CapUsesOverallsColor");
        private static readonly int MultiplyColor = Shader.PropertyToID("_MultiplyColor");
        #endregion

        //---Private Variables
        private CharacterSpecificPalette skin;
        private EntityRef owner;
        private MaterialPropertyBlock materialBlock;
        private readonly List<Renderer> renderers = new();
        private readonly Dictionary<Material, Material> clonedMaterials = new();

        public void OnValidate() {
            this.SetIfNull(ref sRenderer, UnityExtensions.GetComponentType.Children);
            this.SetIfNull(ref animator, UnityExtensions.GetComponentType.Children);
            this.SetIfNull(ref legacySpriteAnimator, UnityExtensions.GetComponentType.Children);
        }

        public void Awake() {
            // Awake void from MarioPlayerAnimator.cs
            renderers.AddRange(GetComponentsInChildren<MeshRenderer>(true));
            renderers.AddRange(GetComponentsInChildren<SkinnedMeshRenderer>(true));
            foreach (Renderer r in renderers) {
                // Get a copy from all materials.
                List<Material> sharedMaterials = new();
                r.GetSharedMaterials(sharedMaterials);
                for (int i = 0; i < sharedMaterials.Count; i++) {
                    Material material = sharedMaterials[i];
                    if (material == null) {
                        continue;
                    }
                    if (!clonedMaterials.TryGetValue(material, out Material clonedMaterial)) {
                        clonedMaterials[material] = clonedMaterial = Instantiate(material);
                    }
                    sharedMaterials[i] = clonedMaterial;
                }
                r.SetSharedMaterials(sharedMaterials);
            }
            InitializeMaterials();
        }

        public override unsafe void OnActivate(Frame f) {
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            var projectile = f.Unsafe.GetPointer<Projectile>(EntityRef);

            owner = projectile->Owner;
            ResolveOwnerPalette(f);
            ApplyTextureReplacements();
            ApplyPaletteColors();

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
            if (PredictedFrame.Unsafe.TryGetPointer(EntityRef, out Projectile* projectile)) {
                // Fixes EntityRef hijacking. Hopefully.
                owner = projectile->Owner;
                if (skin == null) {
                    ResolveOwnerPalette(PredictedFrame);
                    ApplyPaletteColors();
                }
            }

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

        public void OnDestroy() {
            foreach ((_, var material) in clonedMaterials) {
                if (material) {
                    Destroy(material);
                }
            }
        }

        private void InitializeMaterials() {
            if (textureReplacements == null) {
                return;
            }
            foreach (var replacement in textureReplacements) {
                if (replacement?.Material == null) {
                    continue;
                }
                if (clonedMaterials.TryGetValue(replacement.Material, out var mat)) {
                    replacement.Material = mat;
                }
            }
        }

        private void ApplyTextureReplacements() {
            if (textureReplacements == null) {
                return;
            }
            foreach (var replacement in textureReplacements) {
                if (replacement?.Material == null) {
                    continue;
                }
                Material material = replacement.Material;
                material.SetTexture(MainTex, replacement.AlbedoTexture);
                material.SetTexture(OverallsMask, replacement.OverallsMaskTexture);
                material.SetTexture(ShirtMask, replacement.ShirtMaskTexture);
                material.SetTexture(CapMask, replacement.CapMaskTexture);
            }
        }

        private void ApplyPaletteColors() {
            if (renderers.Count == 0) {
                return;
            }
            materialBlock ??= new();
            materialBlock.SetColor(OverallsColor, skin?.OverallsColor.AsColor ?? Color.clear);
            materialBlock.SetColor(ShirtColor, skin?.ShirtColor.AsColor ?? Color.clear);
            materialBlock.SetFloat(CapUsesOverallsColor, (skin?.HatUsesOverallsColor ?? false) ? 1 : 0);
            foreach (Renderer r in renderers) {
                if (r) {
                    r.SetPropertyBlock(materialBlock);
                }
            }
        }

        private void UpdateModelTeamColor(bool sameTeam) {
            if (renderers.Count == 0) {
                return;
            }
            materialBlock ??= new();
            materialBlock.SetColor(MultiplyColor, sameTeam ? sameTeamColor3D : differentTeamColor3D);
            foreach (Renderer r in renderers) {
                if (r) {
                    r.SetPropertyBlock(materialBlock);
                }
            }
        }

        private unsafe void ResolveOwnerPalette(Frame f) {
            skin = null;
            if (!f.Exists(owner) || !f.Unsafe.TryGetPointer(owner, out MarioPlayer* ownerMario)) {
                return;
            }
            var playerData = QuantumUtils.GetPlayerData(f, ownerMario->PlayerRef);
            if (playerData == null || !f.TryFindAsset(playerData->Palette, out PaletteSet palette)) {
                return;
            }
            skin = palette.GetPaletteForCharacter(ownerMario->CharacterAsset);
        }

        private unsafe void OnBeginCameraRendering(ScriptableRenderContext src, Camera camera) {
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
                bool sameTeam = IsCameraTeamFocus(camera);
                if (sRenderer) {
                    sRenderer.color = sameTeam ? sameTeamColor : differentTeamColor;
                }
                UpdateModelTeamColor(sameTeam);
            } catch {
                // Debug.LogWarning("The bug happened");
            }
        }

        private unsafe bool IsCameraTeamFocus(Camera camera) {
            if (!PredictedFrame.Unsafe.TryGetPointer(owner, out MarioPlayer* ownerMario)) {
                return false;
            }

            foreach (var playerElement in PlayerElements.AllPlayerElements) {
                if (playerElement.IsOurCamera(camera)) {
                    // This camera.
                    if (!PredictedFrame.Unsafe.TryGetPointer(playerElement.Entity, out MarioPlayer* cameraMario)) {
                        return false;
                    }

                    bool sameTeam = cameraMario->GetTeam(PredictedFrame) == ownerMario->GetTeam(PredictedFrame);
                    if (!sameTeam) {
                        return false;
                    }

                    if (PredictedFrame.Global->Rules.TeamAttack == TeamAttackOptions.Full) {
                        return owner == playerElement.Entity;
                    }

                    return true;
                }
            }
            return false;
        }
    }
}
