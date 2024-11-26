using UnityEngine;
using UnityEngine.Serialization;

public class LoopingSoundPlayer : MonoBehaviour {

    //---Properties
    public bool IsPlaying => audioSource.isPlaying;
    public AudioSource Source => audioSource;
    protected virtual float AudioStart => currentAudio.loopStartSeconds;
    protected virtual float AudioEnd => currentAudio.loopEndSeconds;

    //---Serialized Variables
    [SerializeField] protected AudioSource audioSource;
    [SerializeField, FormerlySerializedAs("currentSong")] protected LoopingSoundData currentAudio;
    [SerializeField] private bool playOnAwake = true;

    public void Start() {
        if (playOnAwake && currentAudio) {
            Play(currentAudio, true);
        }
    }

    public void Update() {
        if (!audioSource.isPlaying || !currentAudio) {
            return;
        }

        if (currentAudio.loopEndSeconds != -1) {
            float time = audioSource.time;

            if (time >= AudioEnd) {
                audioSource.time = AudioStart + (time - AudioEnd);
            }
        }
    }

    public virtual void SetSoundData(LoopingSoundData data) {
        currentAudio = data;
        audioSource.clip = data.clip;
    }

    public virtual void Play(bool restartIfAlreadyPlaying = true) {
        if (!currentAudio) {
            return;
        }

        Play(currentAudio, restartIfAlreadyPlaying);
    }

    public virtual void Play(LoopingSoundData song, bool restartIfAlreadyPlaying = false) {
        if (currentAudio == song && !audioSource.isPlaying && !restartIfAlreadyPlaying) {
            return;
        }

        currentAudio = song;
        audioSource.loop = true;
        audioSource.clip = song.clip;
        audioSource.time = 0;
        audioSource.Play();
    }

    public void Restart() {
        if (currentAudio) {
            Play(currentAudio, true);
        }
    }

    public void Unpause() {
        audioSource.UnPause();
    }

    public void Pause() {
        audioSource.Pause();
    }

    public void Stop() {
        audioSource.Stop();
    }
}
