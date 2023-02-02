using UnityEngine;

public class LoopingMusicManager : MonoBehaviour {

    //---Properties
    private bool _fastMusic;
    public bool FastMusic {
        set {
            if (_fastMusic == value)
                return;

            float scaleFactor = value ? 0.8f : 1.25f;
            float newTime = audioSource.time * scaleFactor;

            if (currentSong.loopEndSample != -1) {
                float songStart = currentSong.loopStartSample * (value ? 0.8f : 1f);
                float songEnd = currentSong.loopEndSample * (value ? 0.8f : 1f);

                if (newTime >= songEnd)
                    newTime = songStart + (newTime - songEnd);
            }

            audioSource.clip = value && currentSong.fastClip ? currentSong.fastClip : currentSong.clip;
            audioSource.time = newTime;
            audioSource.Play();

            _fastMusic = value;
        }
        get => currentSong.fastClip && _fastMusic;
    }

    //---Serialized Variables
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private MusicData currentSong;

    public void Start() {
        if (currentSong)
            Play(currentSong, true);
    }

    public void Update() {
        if (!audioSource.isPlaying)
            return;

        if (currentSong.loopEndSample != -1) {
            float time = audioSource.time;
            float songStart = currentSong.loopStartSample * (FastMusic ? 0.8f : 1f);
            float songEnd = currentSong.loopEndSample * (FastMusic ? 0.8f : 1f);

            if (time >= songEnd)
                audioSource.time = songStart + (time - songEnd);
        }
    }

    public void Play(MusicData song, bool restartIfAlreadyPlaying = false) {
        if (currentSong == song && !restartIfAlreadyPlaying)
            return;

        currentSong = song;
        audioSource.loop = true;
        audioSource.clip = _fastMusic && song.fastClip ? song.fastClip : song.clip;
        audioSource.time = 0;
        audioSource.Play();
    }

    public void Stop() {
        audioSource.Stop();
    }
}