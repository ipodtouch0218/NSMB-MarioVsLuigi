using NSMB.Extensions;
using Photon.Deterministic;
using Quantum;
using System.Collections.Generic;
using UnityEngine;

public class MarioBrosPlatformAnimator : MonoBehaviour {

    //---Static Variables
    private static readonly Vector2 BumpOffset = new(-0.25f, -0.5f);
    private static readonly Color BlankColor = new(0, 0, 0, 255);
    private static readonly int ParamPlatformWidth = Shader.PropertyToID("PlatformWidth");
    private static readonly int ParamPointsPerTile = Shader.PropertyToID("PointsPerTile");
    private static readonly int ParamDisplacementMap = Shader.PropertyToID("DisplacementMap");

    //---Serialized Variables
    [SerializeField] private QuantumEntityView entity;
    [SerializeField] private SpriteRenderer sRenderer;
    [SerializeField, Delayed] private int samplesPerTile = 8, bumpWidthPoints = 3, bumpBlurPoints = 6;
    [SerializeField] private float bumpDuration = 0.4f;

    //---Misc Variables
    private int platformWidth;
    private Color32[] pixels;
    private MaterialPropertyBlock mpb;
    private Texture2D displacementMap;
    private bool initialized; 

    private readonly List<BumpInfo> bumps = new();
    private VersusStageData stage;

    public void Start() {
        QuantumEvent.Subscribe<EventMarioBrosPlatformBumped>(this, OnMarioBrosPlatformBumped);
        stage = (VersusStageData) QuantumUnityDB.GetGlobalAsset(FindObjectOfType<QuantumMapData>().Asset.UserAsset);
    }

    public void OnValidate() {
        this.SetIfNull(ref entity);
        this.SetIfNull(ref sRenderer, UnityExtensions.GetComponentType.Children);
    }

    public void Initialize(QuantumGame game) {
        Frame f = game.Frames.Predicted;
        var collider = f.Get<PhysicsCollider2D>(entity.EntityRef);

        platformWidth = (collider.Shape.Box.Extents.X * 2).AsInt;

        sRenderer.size = new Vector2(platformWidth, sRenderer.size.y);

        displacementMap = new(platformWidth * samplesPerTile, 1);
        pixels = new Color32[platformWidth * samplesPerTile];

        sRenderer.GetPropertyBlock(mpb = new());
        mpb.SetFloat(ParamPlatformWidth, platformWidth);
        mpb.SetFloat(ParamPointsPerTile, samplesPerTile);

        initialized = true;
    }

    public void Update() {
        if (!initialized) {
            return;
        }

        for (int i = 0; i < platformWidth * samplesPerTile; i++) {
            pixels[i] = BlankColor;
        }
        foreach (BumpInfo bump in bumps) {
            float percentageCompleted = (Time.time - bump.SpawnTime) / bumpDuration;
            float v = Mathf.Sin(Mathf.PI * percentageCompleted);

            for (int x = -bumpWidthPoints - bumpBlurPoints; x <= bumpWidthPoints + bumpBlurPoints; x++) {
                int index = bump.Point + x;
                if (index < 0 || index >= platformWidth * samplesPerTile) {
                    continue;
                }

                float color = v;
                if (x < -bumpWidthPoints || x > bumpWidthPoints) {
                    color *= Mathf.SmoothStep(1, 0, (float) (Mathf.Abs(x) - bumpWidthPoints) / bumpBlurPoints);
                }

                if ((pixels[index].r / 255f) >= color) {
                    continue;
                }

                pixels[index].r = (byte) (Mathf.Clamp01(color) * 255);
            }
        }
        bumps.RemoveAll(bump => Time.time > bump.SpawnTime + bumpDuration);

        displacementMap.SetPixels32(pixels);
        displacementMap.Apply();

        mpb.SetTexture(ParamDisplacementMap, displacementMap);
        sRenderer.SetPropertyBlock(mpb);
    }

    private void OnMarioBrosPlatformBumped(EventMarioBrosPlatformBumped e) {
        if (e.Entity != entity.EntityRef) {
            return;
        }

        Frame f = e.Frame;
        Transform2D qTransform = f.Get<Transform2D>(e.Entity);
        PhysicsCollider2D qCollider = f.Get<PhysicsCollider2D>(e.Entity);

        FPVector2 localPos = qTransform.InverseTransformPoint(e.Position);
        localPos = QuantumUtils.WrapWorld(stage, localPos, out _ );

        float posX = localPos.X.AsFloat;
        float width = qCollider.Shape.Box.Extents.X.AsFloat * 2;

        posX /= width;
        posX += 0.5f; // Get rid of negative coords
        posX = Mathf.Clamp01(posX);
        posX *= samplesPerTile * width;

        foreach (BumpInfo bump in bumps) {
            // If we're too close to another bump, don't create a new one.
            if (Mathf.Abs(bump.Point - posX) < bumpWidthPoints + bumpBlurPoints) {
                return;
            }
        }

        bumps.Add(new BumpInfo() {
            Point = (int) posX,
            SpawnTime = Time.time
        });
    }

    //---Helpers
    private struct BumpInfo {
        public int Point;
        public float SpawnTime;
    }
}
