using NSMB.Extensions;
using Quantum;
using UnityEngine;
using UnityEngine.UI;

public abstract class TrackIcon : QuantumCallbacks {

    //---Serialized Variables
    [SerializeField] private float trackMinX, trackMaxX;
    [SerializeField] protected Image image;

    //---Private Variables
    protected EntityRef targetEntity;
    protected Transform targetTransform;
    protected VersusStageData stage;
    private float levelWidthReciprocal;
    private float levelMinX;
    private float trackWidth;

    public void OnValidate() {
        this.SetIfNull(ref image);
    }

    public void Initialize(EntityRef targetEntity, Transform targetTransform, VersusStageData stage) {
        this.targetEntity = targetEntity;
        this.targetTransform = targetTransform;
        this.stage = stage;
        levelMinX = stage.StageWorldMin.X.AsFloat;
        trackWidth = trackMaxX - trackMinX;
        levelWidthReciprocal = 2f / stage.TileDimensions.x;
    }

    public virtual void LateUpdate() {
        float percentage = (targetTransform.position.x - levelMinX) * levelWidthReciprocal;
        transform.localPosition = new(percentage * trackWidth - trackMaxX, transform.localPosition.y, transform.localPosition.z);
    }
}