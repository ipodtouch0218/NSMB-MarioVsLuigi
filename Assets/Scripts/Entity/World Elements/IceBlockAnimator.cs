using NSMB.Extensions;
using Quantum;
using UnityEngine;

public unsafe class IceBlockAnimator : QuantumEntityViewComponent {

    //---Serialized Variables
    [SerializeField] private AudioSource sfx;
    [SerializeField] private SpriteRenderer sRenderer;
    [SerializeField] private GameObject breakPrefab;

    [SerializeField] private float shakeSpeed = 120, shakeAmount = 0.03f;

    public void OnValidate() {
        this.SetIfNull(ref sfx);
        this.SetIfNull(ref sRenderer, UnityExtensions.GetComponentType.Children);
    }

    public void Start() {
        QuantumEvent.Subscribe<EventIceBlockSinking>(this, OnIceBlockSinking, NetworkHandler.FilterOutReplayFastForward);
    }

    public override void OnActivate(Frame f) {
        var cube = f.Unsafe.GetPointer<IceBlock>(EntityRef);

        sfx.PlayOneShot(SoundEffect.Enemy_Generic_Freeze);
        sRenderer.size = cube->Size.ToUnityVector2() * 2;

        Vector3 position = transform.position;
        position.z = -4.25f;
        transform.position = position;
    }

    public override void OnUpdateView() {
        Frame f = PredictedFrame;
        if (!f.Exists(EntityRef)) {
            return;
        }

        var cube = f.Unsafe.GetPointer<IceBlock>(EntityRef);

        if (cube->AutoBreakFrames > 0 && cube->AutoBreakFrames < 60
            && cube->TimerEnabled(f, EntityRef)) {

            Vector3 position = transform.position;
            float time = (cube->AutoBreakFrames - Game.InterpolationFactor) / 60f;
            position.x += Mathf.Sin(time * shakeSpeed) * shakeAmount;
            transform.position = position;
        }
    }

    public void Destroyed(QuantumGame game) {
        Instantiate(breakPrefab, transform.position, Quaternion.identity);
    }

    private void OnIceBlockSinking(EventIceBlockSinking e) {
        if (e.Entity != EntityRef) {
            return;
        }

        if (e.LiquidType == LiquidType.Lava) {
            sfx.PlayOneShot(SoundEffect.Enemy_Generic_FreezeMelt);
        }
    }
}