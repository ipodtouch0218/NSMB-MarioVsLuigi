using NSMB.Extensions;
using Quantum;
using UnityEngine;

public class BobombAnimator : MonoBehaviour {

    //---Static Variables
    private static readonly int ParamLit = Animator.StringToHash("lit");
    private static readonly int ParamTurnaround = Animator.StringToHash("turnaround");
    private static readonly int ParamFlashAmount = Shader.PropertyToID("FlashAmount");

    //---Serialized Variables
    [SerializeField] private QuantumEntityView entity;
    [SerializeField] private AudioSource sfx;
    [SerializeField] private SpriteRenderer sRenderer;
    [SerializeField] private Animator animator;
    [SerializeField] private GameObject explosionPrefab;

    //---Private Variables
    private MaterialPropertyBlock mpb;

    public void OnValidate() {
        this.SetIfNull(ref entity);
        this.SetIfNull(ref sfx);
        this.SetIfNull(ref sRenderer, UnityExtensions.GetComponentType.Children);
        this.SetIfNull(ref animator, UnityExtensions.GetComponentType.Children);
    }

    public void Start() {
        QuantumCallback.Subscribe<CallbackUpdateView>(this, OnUpdateView);
        QuantumEvent.Subscribe<EventBobombExploded>(this, OnBobombExploded);
        QuantumEvent.Subscribe<EventBobombLit>(this, OnBobombLit);

        sRenderer.GetPropertyBlock(mpb = new());
    }

    private void OnUpdateView(CallbackUpdateView e) {
        QuantumGame game = e.Game;
        Frame f = game.Frames.Predicted;
        if (!f.Exists(entity.EntityRef)) {
            return;
        }

        Bobomb bobomb = f.Get<Bobomb>(entity.EntityRef);
        Enemy enemy = f.Get<Enemy>(entity.EntityRef);

        bool lit = bobomb.CurrentDetonationFrames > 0;
        animator.SetBool(ParamLit, lit);

        if (!lit) {
            mpb.SetFloat(ParamFlashAmount, 0);
        } else {
            float detonationTimer = bobomb.CurrentDetonationFrames / 60f;
            float redOverlayPercent = 5.39f / (detonationTimer + 2.695f) * 10f % 1f;
            mpb.SetFloat(ParamFlashAmount, redOverlayPercent);
        }

        // Bodge...
        if (!enemy.IsAlive) {
            sfx.Stop();
        }

        sRenderer.SetPropertyBlock(mpb);
        sRenderer.enabled = enemy.IsActive;
        sRenderer.flipX = !enemy.FacingRight;
    }

    private void OnBobombExploded(EventBobombExploded e) {
        if (e.Entity != entity.EntityRef) {
            return;
        }

        Instantiate(explosionPrefab, sRenderer.bounds.center, Quaternion.identity);
        sfx.Stop();
    }

    private void OnBobombLit(EventBobombLit e) {
        if (e.Entity != entity.EntityRef) {
            return;
        }

        sfx.clip = SoundEffect.Enemy_Bobomb_Fuse.GetClip();
        sfx.Play();
    }
}