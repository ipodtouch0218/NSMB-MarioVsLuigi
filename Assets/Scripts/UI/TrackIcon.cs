using UnityEngine;
using UnityEngine.UI;

using NSMB.Game;
using NSMB.Extensions;

public abstract class TrackIcon : MonoBehaviour {

    //---Public Variables
    public GameObject target;

    //---Serialized Variables
    [SerializeField] private float trackMinX, trackMaxX;
    [SerializeField] protected Image image;

    //---Private Variables
    private float levelWidthReciprocal;
    private float levelMinX;
    private float trackWidth;

    public void OnValidate() {
        this.SetIfNull(ref image);
    }

    public void Awake() {
        GameManager gm = GameManager.Instance;
        levelMinX = gm.LevelMinX;
        trackWidth = trackMaxX - trackMinX;
        levelWidthReciprocal = 2f / gm.levelWidthTile;
    }

    public virtual void LateUpdate() {
        float percentage = (target.transform.position.x - levelMinX) * levelWidthReciprocal;
        transform.localPosition = new(percentage * trackWidth - trackMaxX, transform.localPosition.y, transform.localPosition.z);
    }
}
