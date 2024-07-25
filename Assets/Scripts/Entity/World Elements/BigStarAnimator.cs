using NSMB.Extensions;
using Quantum;
using UnityEngine;

public class BigStarAnimator : QuantumCallbacks {

    //---Static Variables
    private static Color UncollectableColor = new(1, 1, 1, 0.55f);

    //---Serialized Variables
    [SerializeField] private float pulseAmount = 0.2f, pulseSpeed = 0.2f, rotationSpeed = 30f, blinkingSpeed = 0.5f;
    [SerializeField] private Transform graphicTransform;
    [SerializeField] private ParticleSystem particles;
    [SerializeField] private GameObject starCollectPrefab;

    //---Components
    [SerializeField] private QuantumEntityView entity;
    [SerializeField] public SpriteRenderer sRenderer;
    [SerializeField] private BoxCollider2D worldCollider;
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource sfx, sfx2;

    //--Private Variables
    private float pulseEffectCounter;
    private bool stationary;

    public void OnValidate() {
        this.SetIfNull(ref sRenderer, UnityExtensions.GetComponentType.Children);
        this.SetIfNull(ref worldCollider);
        this.SetIfNull(ref animator);
        this.SetIfNull(ref entity);
        this.SetIfNull(ref sfx);
    }

    public unsafe void Initialize(QuantumGame game) {
        Frame f = game.Frames.Verified;
        var star = f.Get<BigStar>(entity.EntityRef);

        stationary = star.IsStationary;
        if (f.Global->GameState == GameState.Playing) {
            sfx2.PlayOneShot(SoundEffect.World_Star_Spawn);
        }
    }

    public void Update() {
        if (stationary) {
            pulseEffectCounter += Time.deltaTime;
            float sin = Mathf.Sin(pulseEffectCounter * pulseSpeed) * pulseAmount;
            graphicTransform.localScale = Vector3.one * 3f + new Vector3(sin, sin, 0);
        }
    }

    public override void OnUpdateView(QuantumGame game) {
        Frame f = game.Frames.Predicted;
        var star = f.Get<BigStar>(entity.EntityRef);

        if (!stationary) {
            graphicTransform.Rotate(new(0, 0, rotationSpeed * 30 * (star.FacingRight ? -1 : 1) * Time.deltaTime), Space.Self);
            float timeRemaining = star.Lifetime / 60f;
            sRenderer.enabled = !(timeRemaining < 5 && timeRemaining * 2 % (blinkingSpeed * 2) < blinkingSpeed);
        }
    }
}