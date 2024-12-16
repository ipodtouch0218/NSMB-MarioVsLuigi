using UnityEngine;
using Quantum;

public class LoopingMusicData : AssetObject {

    public AudioClip clip;
    public float loopStartSeconds;
    public float loopEndSeconds;
    public AudioClip fastClip;
    public float speedupFactor = 1.25f;

}