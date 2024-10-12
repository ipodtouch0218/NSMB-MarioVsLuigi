using NSMB.Extensions;
using Photon.Deterministic;
using Quantum;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public unsafe class CloudAnimator : MonoBehaviour {

    //---Static Variables
    private static readonly int ParamDisplacementMap = Shader.PropertyToID("DisplacementMap");
    private static readonly int ParamPlatformWidth = Shader.PropertyToID("PlatformWidth");

    //---Serialized Variables
    [SerializeField] private QuantumEntityView entity;
    [SerializeField] private SpriteRenderer sRenderer;

    [SerializeField, Delayed] private int samplesPerTile = 8;
    [SerializeField] private float time = 0.25f;

    //---Private Variables
    private readonly List<CloudContact> positions = new();
    private Color32[] pixels;
    private MaterialPropertyBlock mpb;
    private Texture2D displacementMap;

    public void OnValidate() {
        this.SetIfNull(ref entity);
        this.SetIfNull(ref sRenderer);
    }

    public void Start() {
        QuantumCallback.Subscribe<CallbackUpdateView>(this, OnUpdateView);
    }

    public void Initialize(QuantumGame game) {
        Frame f = game.Frames.Predicted;

        FP width = f.Unsafe.GetPointer<PhysicsCollider2D>(entity.EntityRef)->Shape.Edge.Extent * 2;
        int samples = (int) (width * samplesPerTile);

        displacementMap = new(samples, 1) {
            wrapMode = TextureWrapMode.Mirror
        };

        pixels = new Color32[samples];
        for (int i = 0; i < samples; i++) {
            pixels[i] = new Color32(0, 0, 0, 255);
        }

        displacementMap.SetPixels32(pixels);
        displacementMap.Apply();

        sRenderer.GetPropertyBlock(mpb = new());
        mpb.SetFloat(ParamPlatformWidth, width.AsFloat);
        sRenderer.SetPropertyBlock(mpb);
    }

    public void OnUpdateView(CallbackUpdateView e) {
        QuantumGame game = e.Game;
        Frame f = game.Frames.Predicted;
        Frame fp = game.Frames.PredictedPrevious;

        var filter = f.Filter<PhysicsObject>();
        while (filter.Next(out EntityRef filteredEntity, out PhysicsObject physicsObject)) {
            CloudContact cloudContact = GetContact(filteredEntity);

            if (!physicsObject.DisableCollision
                && f.TryResolveList(physicsObject.Contacts, out var contacts)
                && contacts.Any(c => c.Entity == entity.EntityRef)) {

                // Touching
                if (cloudContact == null) {
                    positions.Add(new CloudContact(filteredEntity, this));
                }
            } else if (cloudContact != null) {
                cloudContact.Exit = true;
            }
        }

        // Update collision timers
        foreach (CloudContact contact in positions) {
            contact.Exit |= !f.Exists(contact.Entity);
            if (contact.Exit) {
                contact.Timer += Time.deltaTime * 0.333f;
            } else {
                contact.Timer = Mathf.Max(0, contact.Timer - Time.deltaTime);
            }
        }

        // Purge old collisions
        for (int i = positions.Count - 1; i >= 0; i--) {
            CloudContact contact = positions[i];
            if (contact.Exit && contact.Timer >= time) {
                positions.RemoveAt(i);
            }
        }

        // Handle graphics
        for (int i = 0; i < pixels.Length; i++) {
            pixels[i].r = 0;
        }

        foreach (CloudContact contact in positions) {
            float percentageCompleted = 1f - (contact.Timer / time);
            float v = Mathf.Sin(Mathf.PI * 0.5f * percentageCompleted);

            FPVector2 lerpedPosition = default;
            if (!contact.Exit) {
                FPVector2 currentPosition = f.Unsafe.GetPointer<Transform2D>(contact.Entity)->Position;
                FPVector2 previousPosition = fp.Unsafe.GetPointer<Transform2D>(contact.Entity)->Position;
                lerpedPosition = QuantumUtils.WrappedLerp(f, previousPosition, currentPosition, FP.FromFloat_UNSAFE(game.InterpolationFactor));
            }

            int point = contact.Point(lerpedPosition.ToUnityVector3(), this);
            int width = contact.Width(f, this);
            for (int x = -width; x <= width; x++) {
                float color = v;
                int localPoint = point + x;
                if (localPoint < 0 || localPoint >= pixels.Length) {
                    continue;
                }

                color *= Mathf.SmoothStep(1, 0, ((float) Mathf.Abs(x)) / width);
                byte final = (byte) (Mathf.Clamp01(color) * 255);

                if (pixels[localPoint].r > final) {
                    continue;
                }

                pixels[localPoint].r = final;
            }
        }

        displacementMap.SetPixels32(pixels);
        displacementMap.Apply();

        mpb.SetTexture(ParamDisplacementMap, displacementMap);
        sRenderer.SetPropertyBlock(mpb);
    }

    private CloudContact GetContact(EntityRef entity) {
        return positions.Where(c => !c.Exit).FirstOrDefault(c => c.Entity == entity);
    }

    public class CloudContact {
        public EntityRef Entity;
        public float Timer;
        public bool Exit;
        public int LastPoint;
        public int LastWidth;

        public CloudContact(EntityRef entity, CloudAnimator cloud) {
            Entity = entity;
            Timer = 0.05f;
            LastWidth = (int) (1.75f * cloud.samplesPerTile);
        }

        public int Width(Frame f, CloudAnimator cloud) {
            if (f.TryGet(Entity, out PhysicsCollider2D collider)) {
                return LastWidth = (int) (collider.Shape.Box.Extents.X.AsFloat * cloud.transform.lossyScale.x * 8f * cloud.samplesPerTile);
            }

            return LastWidth;
        }

        public int Point(Vector3 entityPosition, CloudAnimator cloud) {
            if (Exit) {
                return LastPoint;
            }

            return LastPoint = (int) (cloud.transform.InverseTransformPoint(entityPosition).x * cloud.samplesPerTile);
        }
    }
}
