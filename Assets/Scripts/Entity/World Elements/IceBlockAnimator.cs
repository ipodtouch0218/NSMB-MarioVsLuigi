using NSMB.Extensions;
using Quantum;
using UnityEngine;
using UnityEngine.UIElements;

public class IceBlockAnimator : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private QuantumEntityView entity;
    [SerializeField] private AudioSource sfx;
    [SerializeField] private SpriteRenderer sRenderer;
    [SerializeField] private GameObject breakPrefab;

    [SerializeField] private float shakeSpeed = 120, shakeAmount = 0.03f;

    public void OnValidate() {
        this.SetIfNull(ref entity);
        this.SetIfNull(ref sfx);
        this.SetIfNull(ref sRenderer, UnityExtensions.GetComponentType.Children);
    }

    public void Start() {
        QuantumCallback.Subscribe<CallbackUpdateView>(this, OnUpdateView);
    }

    public void Initialize(QuantumGame game) {
        Frame f = game.Frames.Predicted;
        var cube = f.Get<IceBlock>(entity.EntityRef);

        sfx.PlayOneShot(SoundEffect.Enemy_Generic_Freeze);
        sRenderer.size = cube.Size.ToUnityVector2() * 2;

        Vector3 position = transform.position;
        position.z = -4.25f;
        transform.position = position;
    }

    public void OnUpdateView(CallbackUpdateView e) {
        Frame f = e.Game.Frames.Predicted;
        if (!f.Exists(entity.EntityRef)) {
            return;
        }

        var cube = f.Get<IceBlock>(entity.EntityRef);

        if (cube.AutoBreakFrames > 0 && cube.AutoBreakFrames < 60
            && cube.TimerEnabled(f, entity.EntityRef)) {

            Vector3 position = transform.position;
            float time = (cube.AutoBreakFrames - e.Game.InterpolationFactor) / 60f;
            position.x += Mathf.Sin(time * shakeSpeed) * shakeAmount;
            transform.position = position;
        }
    }

    public void Destroyed(QuantumGame game) {
        Instantiate(breakPrefab, transform.position, Quaternion.identity);
    }
}