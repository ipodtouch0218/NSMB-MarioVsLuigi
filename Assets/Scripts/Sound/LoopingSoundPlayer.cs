using UnityEngine;
using UnityEngine.Serialization;
using NSMB.Utilities;

namespace NSMB.Sound {
    public class LoopingSoundPlayer : MonoBehaviour {

        //---Properties
        public int gameStyle => (int) Utils.GetStageTheme();
        public bool IsPlaying => audioSource.isPlaying;
        public AudioSource Source => audioSource;
        protected virtual float AudioStart => currentAudio.loopStartSeconds[gameStyle];
        protected virtual float AudioEnd => currentAudio.loopEndSeconds[gameStyle];

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

            if (currentAudio.loopEndSeconds[gameStyle] != -1) {
                float time = audioSource.time;

                if (time >= AudioEnd) {
                    audioSource.time = AudioStart + (time - AudioEnd);
                }
            }
        }

        public virtual void SetSoundData(LoopingSoundData data) {
            currentAudio = data;
            audioSource.clip = data.clip[gameStyle];
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
            audioSource.clip = song.clip[(int) Utils.GetStageTheme()];
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
}