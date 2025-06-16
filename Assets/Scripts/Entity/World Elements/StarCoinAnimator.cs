using UnityEngine;
using Quantum;
using NSMB.Extensions;

public class StarCoinAnimator : QuantumEntityViewComponent {

    //---Serialized Variables
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource sfx;
    [SerializeField] private MeshRenderer mRenderer;
    [SerializeField] private ParticleSystem particles;

    public void OnValidate() {
        this.SetIfNull(ref animator);
        this.SetIfNull(ref sfx);
        this.SetIfNull(ref mRenderer, UnityExtensions.GetComponentType.Children);
    }

    public void Start() {
        QuantumEvent.Subscribe<EventMarioPlayerCollectedStarCoin>(this, OnMarioPlayerCollectedStarCoin);
        EntityView.OnEntityDestroyed.AddListener(OnEntityDestroyed);
    }

    public void OnDestroy() {
        EntityView.OnEntityDestroyed.RemoveListener(OnEntityDestroyed);
    }

    public void OnEntityDestroyed(QuantumGame game) {
        if (!NetworkHandler.IsReplayFastForwarding) {
            sfx.PlayOneShot(SoundEffect.World_Starcoin_Store);
        }
        mRenderer.enabled = false;
        Destroy(gameObject, SoundEffect.World_Starcoin_Store.GetClip().length + 1);
    }

    private void OnMarioPlayerCollectedStarCoin(EventMarioPlayerCollectedStarCoin e) {
        if (e.StarCoinEntity != EntityRef) {
            return;
        }

        animator.SetTrigger("collected");
        particles.Play();
        if (!NetworkHandler.IsReplayFastForwarding) {
            sfx.Play();
        }
    }
}