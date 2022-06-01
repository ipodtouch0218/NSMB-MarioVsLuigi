using UnityEngine;

public class LoopingMusic : MonoBehaviour {

    public AudioSource audioSource;
    public MusicData currentSong;

    public void Start() {
        if (currentSong)
            Play(currentSong);
    }

    public void Play(MusicData song) {
        currentSong = song;
        audioSource.loop = true;
        audioSource.clip = song.clip;
        audioSource.time = 0;
        audioSource.Play();
    }
    public void Stop() {
        audioSource.Stop();
    }

    public void Update() {
        if (!audioSource.isPlaying)
            return;

        if (currentSong.loopEndSample != -1 && audioSource.time >= currentSong.loopEndSample)
            audioSource.time = currentSong.loopStartSample + (audioSource.time - currentSong.loopEndSample);
    }

}