using UnityEngine;
using Quantum;
using NSMB.Extensions;
using System;
using NSMB.UI.Game;
using UnityEngine.Rendering;
using NSMB.Utils;

public class StarCoinAnimator : QuantumEntityViewComponent {

    //---Static
    public static event Action<Frame, StarCoinAnimator> StarCoinInitialized;
    public static event Action<Frame, StarCoinAnimator> StarCoinDestroyed;

    //---Serialized Variables
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource sfx;
    [SerializeField] private MeshRenderer mRenderer;
    [SerializeField] private ParticleSystem particles;
    [SerializeField] private Material solidMaterial, transparentMaterial;

    public void OnValidate() {
        this.SetIfNull(ref animator);
        this.SetIfNull(ref sfx);
        this.SetIfNull(ref mRenderer, UnityExtensions.GetComponentType.Children);
    }

    public void Start() {
        QuantumEvent.Subscribe<EventMarioPlayerCollectedStarCoin>(this, OnMarioPlayerCollectedStarCoin);
        EntityView.OnEntityDestroyed.AddListener(OnEntityDestroyed);
        RenderPipelineManager.beginCameraRendering += URPOnPreRender;
    }

    public override unsafe void OnActivate(Frame f) {
        if (f.Global->GameState == GameState.Playing && !NetworkHandler.IsReplayFastForwarding) {
            GlobalController.Instance.sfx.PlayOneShot(SoundEffect.World_Star_Spawn);
        }
        StarCoinInitialized?.Invoke(f, this);
    }

    public void OnDestroy() {
        EntityView.OnEntityDestroyed.RemoveListener(OnEntityDestroyed);
        RenderPipelineManager.beginCameraRendering -= URPOnPreRender;
    }

    public void OnEntityDestroyed(QuantumGame game) {
        if (!NetworkHandler.IsReplayFastForwarding) {
            sfx.PlayOneShot(SoundEffect.World_Starcoin_Store);
        }
        mRenderer.enabled = false;
        Destroy(gameObject, SoundEffect.World_Starcoin_Store.GetClip().length + 1);
    }

    private unsafe void URPOnPreRender(ScriptableRenderContext context, Camera camera) {
        Frame f = PredictedFrame;
        if (f == null || !f.Unsafe.TryGetPointer(EntityRef, out StarCoin* starCoin)) {
            return;
        }

        bool solid = !f.Exists(starCoin->Collector) || IsCollectedByCameraFocus(starCoin->Collector, camera);
        mRenderer.sharedMaterial = solid ? solidMaterial : transparentMaterial;
    }

    private bool IsCollectedByCameraFocus(EntityRef entity, Camera camera) {
        foreach (var playerElement in PlayerElements.AllPlayerElements) {
            if (camera == playerElement.Camera || camera == playerElement.ScrollCamera || camera == playerElement.UICamera) {
                return playerElement.Entity == entity;
            }
        }
        return false;
    }

    private void OnMarioPlayerCollectedStarCoin(EventMarioPlayerCollectedStarCoin e) {
        if (e.StarCoinEntity != EntityRef) {
            return;
        }
        
        animator.SetTrigger("collected");
        particles.Play();
        if (!NetworkHandler.IsReplayFastForwarding) {
            sfx.Play();
            if (!Utils.IsMarioLocal(e.Entity)) {
                GlobalController.Instance.sfx.PlayOneShot(SoundEffect.World_Star_CollectOthers);
            }
        }
        StarCoinDestroyed?.Invoke(VerifiedFrame, this);
    }
}