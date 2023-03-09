using UnityEngine;

[CreateAssetMenu(fileName = "LoopingMusicData", menuName = "ScriptableObjects/LoopingSound/MusicData")]
public class LoopingMusicData : LoopingSoundData {

    public AudioClip fastClip;
    public float speedupFactor = 1.25f;

}
