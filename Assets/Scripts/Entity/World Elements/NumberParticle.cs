using UnityEngine;
using UnityEngine.Serialization;
using TMPro;
using NSMB.Extensions;

public class NumberParticle : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private TMP_Text text;
    [SerializeField] private Vector3 colorOffset;
    [SerializeField] private Color overlay;

    //---Components
    [SerializeField] private AnimationCurve yMovement;
    [SerializeField, FormerlySerializedAs("animation")] private Animation legacyAnimation;

    //---Private Variables
    private MaterialPropertyBlock mpb;
    private MeshRenderer mr;

    private float spawnTime;
    private bool final;

    public void OnValidate() {
        this.SetIfNull(ref text, UnityExtensions.GetComponentType.Children);
        this.SetIfNull(ref legacyAnimation, UnityExtensions.GetComponentType.Children);
    }

    public void Initialize(string newText, Color color, bool final) {
        this.final = final;

        text.text = newText;
        text.ForceMeshUpdate();
        mr = GetComponentsInChildren<MeshRenderer>()[1];

        mpb = new();
        mpb.SetColor("_Color", color);
        mr.SetPropertyBlock(mpb);

        legacyAnimation.enabled = final;

        spawnTime = Time.time;
        Destroy(gameObject.transform.parent.gameObject, final ? 1.42f : 0.666f);
        Update();
    }

    public void Update() {
        if (final) {
            mpb.SetVector("_ColorOffset", colorOffset);
            mpb.SetColor("_Overlay", overlay);
            mr.SetPropertyBlock(mpb);
        }

        transform.localPosition = Vector3.up * yMovement.Evaluate(Time.time - spawnTime);
    }
}
