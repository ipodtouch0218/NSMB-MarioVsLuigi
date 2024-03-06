public class LoopingMusicPlayer : LoopingSoundPlayer {

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
    protected override float AudioStart => currentAudio.loopStartSeconds * CurrentSpeedupFactor;
    protected override float AudioEnd => currentAudio.loopEndSeconds * CurrentSpeedupFactor;
    private LoopingMusicData CurrentMusicSong => currentAudio as LoopingMusicData;
    private float CurrentSpeedupFactor => FastMusic ? 1f / (CurrentMusicSong?.speedupFactor ?? 1f) : 1f;


    public override void SetSoundData(LoopingSoundData data) {
        currentAudio = data;
        if (data is LoopingMusicData music) {
            audioSource.clip = (_fastMusic && music.fastClip) ? music.fastClip : music.clip;
        } else {
            audioSource.clip = data.clip;
        }
    }

    public override void Play(LoopingSoundData song, bool restartIfAlreadyPlaying = false) {
        if (currentAudio == song && !restartIfAlreadyPlaying) {
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
