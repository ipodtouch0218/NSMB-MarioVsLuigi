using UnityEngine;

[CreateAssetMenu(fileName = "MusicData", menuName = "ScriptableObjects/MusicData")]
public class MusicData : ScriptableObject {

    public AudioClip clip, fastClip;
    public float loopStartSample, loopEndSample;

}