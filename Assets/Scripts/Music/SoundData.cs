using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "SoundData", menuName = "ScriptableObjects/LoopingSound/SoundData")]
public class LoopingSoundData : ScriptableObject {

    public AudioClip clip;

    [FormerlySerializedAs("loopStartSample")]
    public float loopStartSeconds;

    [FormerlySerializedAs("loopEndSample")]
    public float loopEndSeconds;

}
