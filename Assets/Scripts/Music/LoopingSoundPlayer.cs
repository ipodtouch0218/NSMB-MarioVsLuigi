using UnityEngine;
using UnityEngine.Serialization;

public class LoopingSoundPlayer : MonoBehaviour {

    //---Properties
    protected virtual float AudioStart => currentAudio.loopStartSeconds;
    protected virtual float AudioEnd => currentAudio.loopEndSeconds;

    //---Serialized Variables
    [SerializeField] protected AudioSource audioSource;
    [SerializeField, FormerlySerializedAs("currentSong")] protected LoopingSoundData currentAudio;

    public void Start() {
        if (currentAudio)
            Play(currentAudio, true);
    }

    public void Update() {
        if (!audioSource.isPlaying)
            return;

        if (currentAudio.loopEndSeconds != -1) {
            float time = audioSource.time;

            if (time >= AudioEnd)
                audioSource.time = AudioStart + (time - AudioEnd);
        }
    }

    public virtual void SetSoundData(LoopingSoundData data) {
        currentAudio = data;
        audioSource.clip = data.clip;
    }

    public virtual void Play(LoopingSoundData song, bool restartIfAlreadyPlaying = false) {
        if (currentAudio == song && !restartIfAlreadyPlaying)
            return;

        currentAudio = song;
        audioSource.loop = true;
        audioSource.clip = song.clip;
        audioSource.time = 0;
        audioSource.Play();
    }

    public void Stop() {
        audioSource.Stop();
    }
}
