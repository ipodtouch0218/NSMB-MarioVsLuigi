using NSMB.Extensions;
using Quantum;
using System.Linq;
using UnityEngine;

public class MusicManager : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private LoopingMusicPlayer musicPlayer;

    //---Private Variables
    private VersusStageData stage;

    public void OnValidate() {
        this.SetIfNull(ref musicPlayer);
    }

    public void Start() {
        QuantumCallback.Subscribe<CallbackUpdateView>(this, OnUpdateView);
        QuantumEvent.Subscribe<EventMarioPlayerDied>(this, OnMarioPlayerDied);
        QuantumEvent.Subscribe<EventMarioPlayerRespawned>(this, OnMarioPlayerRespawned);
        QuantumEvent.Subscribe<EventGameEnded>(this, OnGameEnded);

        stage = (VersusStageData) QuantumUnityDB.GetGlobalAsset(FindObjectOfType<QuantumMapData>().Asset.UserAsset);
    }

    public void OnUpdateView(CallbackUpdateView e) {
        HandleMusic(e.Game, false);
    }

    private unsafe void HandleMusic(QuantumGame game, bool force) {
        Frame f = game.Frames.Predicted;
        var rules = f.Global->Rules;

        if (!force && !musicPlayer.IsPlaying) {
            return;
        }

        bool invincible = false;
        bool mega = false;
        bool speedup = false;

        var allPlayers = f.Filter<MarioPlayer>();
        int playersWithOneLife = 0;
        int playerCount = 0;
        while (allPlayers.Next(out EntityRef entity, out MarioPlayer mario)) {
            if (rules.IsLivesEnabled) {
                if (mario.Lives == 1) {
                    playersWithOneLife++;
                }
            }
            playerCount++;

            if (!game.PlayerIsLocal(mario.PlayerRef)
                && !PlayerElements.AllPlayerElements.Any(pe => pe.Entity == entity)) {
                continue;
            }

            mega |= Settings.Instance.audioSpecialPowerupMusic.HasFlag(Enums.SpecialPowerupMusic.MegaMushroom) && mario.MegaMushroomFrames > 0;
            invincible |= Settings.Instance.audioSpecialPowerupMusic.HasFlag(Enums.SpecialPowerupMusic.Starman) && mario.IsStarmanInvincible;
        }

        speedup |= rules.TimerSeconds > 0 && f.Global->Timer <= 60;
        speedup |= QuantumUtils.GetFirstPlaceStars(f) >= rules.StarsToWin - 1;

        if (!speedup && rules.IsLivesEnabled) {
            // Also speed up the music if:
            // A: two players left, at least one has one life
            // B: three+ players left, all have one life
            speedup |= (playerCount <= 2 && playersWithOneLife > 0) || (playersWithOneLife >= playerCount);
        }

        if (mega) {
            musicPlayer.Play(stage.MegaMushroomMusic);
        } else if (invincible) {
            musicPlayer.Play(stage.InvincibleMusic);
        } else {
            musicPlayer.Play(stage.MainMusic);
        }

        musicPlayer.FastMusic = speedup;
    }

    private void OnGameEnded(EventGameEnded e) {
        musicPlayer.Stop();
    }

    private void OnMarioPlayerRespawned(EventMarioPlayerRespawned e) {
        if ((e.Game.PlayerIsLocal(e.Mario.PlayerRef) || PlayerElements.AllPlayerElements.Any(pe => pe.Entity == e.Entity))
            && !musicPlayer.IsPlaying) {
            HandleMusic(e.Game, true);
        }
    }

    private void OnMarioPlayerDied(EventMarioPlayerDied e) {
        if (e.Game.PlayerIsLocal(e.Mario.PlayerRef) && Settings.Instance.audioRestartMusicOnDeath) {
            musicPlayer.Stop();
        }
    }
}