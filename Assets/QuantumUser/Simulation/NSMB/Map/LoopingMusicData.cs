using Quantum;

public class LoopingMusicData : AssetObject {

#if QUANTUM_UNITY
    public UnityEngine.AudioClip clip;
    public UnityEngine.AudioClip fastClip;
#endif
    public float loopStartSeconds;
    public float loopEndSeconds;
    public float speedupFactor = 1.25f;

}