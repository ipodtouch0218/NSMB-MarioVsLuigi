using NSMB.Extensions;
using Quantum;
using UnityEngine;

public class FrozenCubeAnimator : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private QuantumEntityView entity;
    [SerializeField] private AudioSource sfx;
    [SerializeField] private SpriteRenderer sRenderer;

    public void OnValidate() {
        this.SetIfNull(ref entity);
        this.SetIfNull(ref sfx);
        this.SetIfNull(ref sRenderer, UnityExtensions.GetComponentType.Children);
    }

    public void Initialize(QuantumGame game) {
        Frame f = game.Frames.Predicted;
        var cube = f.Get<FrozenCube>(entity.EntityRef);

        sfx.PlayOneShot(SoundEffect.Enemy_Generic_Freeze);
        sRenderer.size = cube.Size.ToUnityVector2() * 2;
    }
}