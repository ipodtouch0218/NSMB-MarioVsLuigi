using System;
using UnityEngine;

public class PlaybackSpeedReplayUITab : ReplayUITab {

    //---Serialized Variables
    [SerializeField] private float[] speeds = { 0.25f, 0.5f, 1f, 2f, 4f };

    protected override void OnEnable() {
        base.OnEnable();
        SelectItem(Array.IndexOf(speeds, parent.ReplaySpeed));
    }

    public void ChangePlaybackSpeedViaIndex(int index) {
        parent.ChangeReplaySpeed(index);
        SelectItem(Array.IndexOf(speeds, parent.ReplaySpeed));
    }
}