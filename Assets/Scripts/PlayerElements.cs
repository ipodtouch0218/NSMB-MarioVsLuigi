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
    [SerializeField] private ScoreboardUpdater scoreboardUpdater;

    //---Private Variables
    private PlayerRef player;
    private EntityRef entity;

    public void OnValidate() {
        this.SetIfNull(ref image);
        this.SetIfNull(ref uiUpdater);
        this.SetIfNull(ref cameraAnimator);
        this.SetIfNull(ref ourCamera, UnityExtensions.GetComponentType.Children);
        this.SetIfNull(ref inputCollector);
        this.SetIfNull(ref scoreboardUpdater, UnityExtensions.GetComponentType.Children);
    }

    public void OnEnable() {
        AllPlayerElements.Add(this);
    }

    public void OnDisable() {
        AllPlayerElements.Remove(this);
    }

    public void Initialize(EntityRef entity, PlayerRef player) {
        this.player = player;
        this.entity = entity;
        uiUpdater.Target = entity;
        
        cameraAnimator.Target = entity;
        Camera.transform.SetParent(null);
        Camera.transform.localScale = Vector3.one;

        scoreboardUpdater.Initialize(entity);
    }
}