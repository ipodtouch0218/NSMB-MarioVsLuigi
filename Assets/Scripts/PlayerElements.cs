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

    public void OnValidate() {
        this.SetIfNull(ref image);
        this.SetIfNull(ref uiUpdater);
        this.SetIfNull(ref cameraAnimator);
        this.SetIfNull(ref ourCamera, UnityExtensions.GetComponentType.Children);
        this.SetIfNull(ref inputCollector);
    }

    public void OnEnable() {
        AllPlayerElements.Add(this);
    }

    public void OnDisable() {
        AllPlayerElements.Remove(this);
    }

    public void Initialize(PlayerRef player) {
        this.player = player;
        Camera.transform.SetParent(null);
        Camera.transform.localScale = Vector3.one;
    }

    public void SetEntity(EntityRef entity) {
        this.entity = entity;
        uiUpdater.Target = entity;
        cameraAnimator.Target = entity;
    }
}