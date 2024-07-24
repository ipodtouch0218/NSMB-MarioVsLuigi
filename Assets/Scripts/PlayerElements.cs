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

    //---Serialized Variables
    [SerializeField] private RawImage image;
    [SerializeField] private UIUpdater uiUpdater;
    [SerializeField] private CameraAnimator cameraAnimator;
    [SerializeField] private Camera ourCamera;

    //---Private Variables
    private PlayerRef player;
    private EntityRef entity;
    private RenderTexture texture;

    public void OnValidate() {
        this.SetIfNull(ref image);
        this.SetIfNull(ref uiUpdater);
        this.SetIfNull(ref cameraAnimator);
        this.SetIfNull(ref ourCamera, UnityExtensions.GetComponentType.Children);
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
        uiUpdater.transform.SetParent(null);
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

        texture = RenderTexture.GetTemporary(Screen.currentResolution.width, Screen.currentResolution.height);
        image.texture = texture;

        foreach (var camera in GetComponentsInChildren<Camera>()) {
            camera.targetTexture = texture;
        }
    }
}