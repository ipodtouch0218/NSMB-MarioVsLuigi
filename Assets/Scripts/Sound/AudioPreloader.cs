using Quantum;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NSMB.Sound {
    public class AudioPreloader : MonoBehaviour {

        //---Private Variables
        private readonly HashSet<AudioClip> loadedClips = new();

        public void Start() {
            QuantumCallback.Subscribe<CallbackGameStarted>(this, OnGameStarted);
            QuantumCallback.Subscribe<CallbackGameDestroyed>(this, OnGameDestroyed);
            QuantumEvent.Subscribe<EventGameStateChanged>(this, OnGameStateChanged);

            foreach (SoundEffect sfx in Enum.GetValues(typeof(SoundEffect))) {
                CharacterAsset[] characters = GlobalController.Instance.config.CharacterDatas.Select(QuantumUnityDB.GetGlobalAsset).ToArray();
                PreloadSoundEffect(sfx, characters);
            }
        }

        private unsafe void HandleGameStateChange(QuantumGame game) {
            Frame f = game.Frames.Predicted;
            
            if (f.Global->GameState == GameState.PreGameRoom) {
                // Unload music, starman, and mega mushroom music.
                UnloadMusic();
            } else {
                // Load music, starman, and mega mushroom music.
                LoadMusic(f);
            }
        }

        private void LoadMusic(Frame f) {
            if (!f.Map || !f.TryFindAsset(f.Map.UserAsset, out VersusStageData stage)) {
                return;
            }

            // Main music
            PreloadMusic(f.FindAsset(stage.GetCurrentMusic(f)));
            PreloadMusic(f.FindAsset(stage.MegaMushroomMusic));
            PreloadMusic(f.FindAsset(stage.InvincibleMusic));
        }

        private void PreloadMusic(LoopingMusicData musicData) {
            PreloadClip(musicData.clip);
            PreloadClip(musicData.fastClip);
        }

        private void PreloadClip(AudioClip clip) {
            if (loadedClips.Contains(clip)) {
                return;
            }

            clip.LoadAudioData();
            loadedClips.Add(clip);
        }

        private void UnloadMusic() {
            foreach (var clip in loadedClips) {
                clip.UnloadAudioData();
            }
            loadedClips.Clear();
        }

        private void PreloadSoundEffect(SoundEffect sfx, CharacterAsset[] characters) {
            var data = sfx.GetSoundData();
            if (data.Sound.Contains("{char}")) {
                foreach (var character in characters) {
                    if (data.Variants > 1) {
                        for (int i = 1; i <= data.Variants; i++) {
                            sfx.GetClip(character, variant: i);
                        }
                    } else {
                        sfx.GetClip(character);
                    }
                }
            } else {
                if (data.Variants > 1) {
                    for (int i = 1; i <= data.Variants; i++) {
                        sfx.GetClip(variant: i);
                    }
                } else {
                    sfx.GetClip();
                }
            }
        }

        //---Callbacks & Events
        private void OnGameStarted(CallbackGameStarted e) {
            HandleGameStateChange(e.Game);
        }

        private void OnGameStateChanged(EventGameStateChanged e) {
            HandleGameStateChange(e.Game);
        }

        private void OnGameDestroyed(CallbackGameDestroyed e) {
            UnloadMusic();
        }
    }
}