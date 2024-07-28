using NSMB.Extensions;
using Quantum;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerElements : MonoBehaviour {

    public static HashSet<PlayerElements> AllPlayerElements = new();

    //---Properties
    public PlayerRef Player => player;
    public Camera Camera => ourCamera;
    public CameraAnimator CameraAnimator => cameraAnimator;

    //---Serialized Variables
    [SerializeField] private RawImage image;
    [SerializeField] private UIUpdater uiUpdater;
    [SerializeField] private CameraAnimator cameraAnimator;
    [SerializeField] private Camera ourCamera;
    [SerializeField] private InputCollector inputCollector;

    //---Private Variables
    private PlayerRef player;
    private EntityRef entity;
    private RenderTexture texture;

    public void OnValidate() {
        this.SetIfNull(ref image);
        this.SetIfNull(ref uiUpdater);
        this.SetIfNull(ref cameraAnimator);
        this.SetIfNull(ref ourCamera, UnityExtensions.GetComponentType.Children);
        this.SetIfNull(ref inputCollector);
    }

    public void OnEnable() {
        AllPlayerElements.Add(this);
        GlobalController.ResolutionChanged += OnResolutionChanged;
    }

    public void OnDisable() {
        AllPlayerElements.Remove(this);
        GlobalController.ResolutionChanged -= OnResolutionChanged;
    }

    public void OnDestroy() {
        RenderTexture.ReleaseTemporary(texture);
    }

    public void Initialize(PlayerRef player) {
        this.player = player;
        Camera.transform.SetParent(null);
        Camera.transform.localScale = Vector3.one;
        OnResolutionChanged();
    }

    public void SetEntity(EntityRef entity) {
        this.entity = entity;
        uiUpdater.Target = entity;
        cameraAnimator.Target = entity;
    }

    private void OnResolutionChanged() {
        if (texture) {
            RenderTexture.ReleaseTemporary(texture);
        }

        texture = RenderTexture.GetTemporary(Screen.width, Screen.height);
        image.texture = texture;
        Camera.targetTexture = texture;

        float aspect = ourCamera.aspect;
        double size = (14 / 4f) /*+ SizeIncreaseCurrent*/;

        // https://forum.unity.com/threads/how-to-calculate-horizontal-field-of-view.16114/#post-2961964
        double aspectReciprocals = 1d / aspect;

        foreach (var camera in GetComponentsInChildren<Camera>()) {
            camera.targetTexture = texture;
            camera.orthographicSize = Mathf.Min((float) size, (float) (size * (16d/9d) * aspectReciprocals));
        }
    }
}