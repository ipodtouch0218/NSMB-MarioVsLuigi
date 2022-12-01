﻿using UnityEngine;
using UnityEngine.UI;

public class TrackIcon : MonoBehaviour {

    //---Public Variables
    public GameObject target;

    //---Serialized Variables
    [SerializeField] private float trackMinX, trackMaxX;

    //---Components
    protected Image image;

    //---Private Variables
    private float levelWidthReciprocal;
    private float levelMinX;
    private float trackWidth;

    public void Awake() {
        image = GetComponent<Image>();

        GameManager gm = GameManager.Instance;
        levelMinX = gm.GetLevelMinX();
        trackWidth = trackMaxX - trackMinX;
        levelWidthReciprocal = 2f / gm.levelWidthTile;
    }

    public virtual void Update() {
        float percentage = (target.transform.position.x - levelMinX) * levelWidthReciprocal;
        transform.localPosition = new(percentage * trackWidth - trackMaxX, transform.localPosition.y, transform.localPosition.z);
    }
}
