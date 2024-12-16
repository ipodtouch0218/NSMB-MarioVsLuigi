using UnityEngine;

public class LoopingMusicPlayer : MonoBehaviour {

    //---Properties
    public bool IsPlaying => audioSource.isPlaying;
    public float AudioStart => currentAudio.loopStartSeconds * CurrentSpeedupFactor;
    public float AudioEnd => currentAudio.loopEndSeconds * CurrentSpeedupFactor;
    private LoopingMusicData CurrentMusicSong => currentAudio;
    private float CurrentSpeedupFactor => FastMusic ? 1f / (CurrentMusicSong?.speedupFactor ?? 1f) : 1f;

    //---Serialized Variables
    [SerializeField] protected AudioSource audioSource;
    [SerializeField] protected LoopingMusicData currentAudio;
    [SerializeField] private bool playOnAwake = true;

    public void OnEnable() {
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

    //---Properties
    private bool _fastMusic;
    public bool FastMusic {
        set {
            if (_fastMusic == value) {
                return;
            }

            _fastMusic = value;

            if (!CurrentMusicSong) {
                return;
            }

            float scaleFactor = CurrentMusicSong.speedupFactor;
            if (_fastMusic) {
                scaleFactor = 1f / scaleFactor;
            }

            float time = audioSource.time;
            audioSource.clip = (_fastMusic && CurrentMusicSong.fastClip) ? CurrentMusicSong.fastClip : CurrentMusicSong.clip;
            audioSource.Play();
            audioSource.time = time * scaleFactor;

            Update();
        }
        get => _fastMusic && CurrentMusicSong && CurrentMusicSong.fastClip;
    }


    public void SetSoundData(LoopingMusicData data) {
        currentAudio = data;
        audioSource.clip = (_fastMusic && data.fastClip) ? data.fastClip : data.clip;
    }

    public void Play(LoopingMusicData song, bool restartIfAlreadyPlaying = false) {
        if (currentAudio == song && audioSource.isPlaying && !restartIfAlreadyPlaying) {
            return;
        }

        currentAudio = song;
        audioSource.loop = true;
        if (CurrentMusicSong) {
            audioSource.clip = (_fastMusic && CurrentMusicSong.fastClip) ? CurrentMusicSong.fastClip : CurrentMusicSong.clip;
        } else {
            audioSource.clip = song.clip;
        }
        audioSource.time = 0;
        audioSource.Play();
    }
}
