using Discord;
using NSMB.Networking;
using NSMB.UI.Translation;
using Photon.Realtime;
using Quantum;
using System;
using System.IO;
using UnityEngine;

namespace NSMB {
    public unsafe class DiscordController : MonoBehaviour {
#pragma warning disable IDE0079
#pragma warning disable CS0162

        //---Static Variables
        private static readonly long DiscordAppId = 962073502469459999;

        //---Private Variables
        private Discord.Discord discord;
        private ActivityManager activityManager;
        private float lastInitializeTime;

        public void OnEnable() {
            TranslationManager.OnLanguageChanged += OnLanguageChanged;
            Settings.OnDiscordIntegrationChanged += OnDiscordIntegrationChanged;
        }

        public void OnDisable() {
            TranslationManager.OnLanguageChanged -= OnLanguageChanged;
            Settings.OnDiscordIntegrationChanged -= OnDiscordIntegrationChanged;

            discord?.Dispose();
            discord = null;
        }

        public void Start() {
            Initialize();
        }

        private bool Initialize() {
#if !UNITY_STANDALONE
            enabled = false;
            return false;
#endif

            lastInitializeTime = Time.time;
            try {
                discord = new Discord.Discord(DiscordAppId, (ulong) CreateFlags.NoRequireDiscord);
            } catch {
                return false;
            }
            activityManager = discord.GetActivityManager();
            activityManager.OnActivityJoin += TryJoinGame;

            try {
                string filename = AppDomain.CurrentDomain.ToString();
                filename = string.Join(' ', filename.Split(' ')[..^2]);
                string dir = AppDomain.CurrentDomain.BaseDirectory + Path.DirectorySeparatorChar + filename;
                activityManager.RegisterCommand(dir);
                Debug.Log($"[Discord] Set launch path to \"{dir}\"");
            } catch {
                Debug.LogError($"[Discord] Failed to set launch path (on {Application.platform})");
            }

            return true;
        }

        public void Update() {
            if (discord == null) {
                if (Time.time - lastInitializeTime > 10) {
                    // Try to recreate every 10 seconds
                    Initialize();
                }
                return;
            }

            if ((int) (Time.unscaledTime + Time.unscaledDeltaTime) > (int) Time.unscaledTime) {
                // Update discord status every second
                UpdateActivity();
            }

            try {
                discord.RunCallbacks();
            } catch {
                // Ignored
            }
        }

        public unsafe void UpdateActivity() {
#if UNITY_WEBGL || UNITY_WSA
        return;
#endif
            if (!Application.isPlaying || discord == null) {
                return;
            }

            if (!Settings.Instance.GeneralDiscordIntegration) {
                activityManager.ClearActivity(_ => { });
                return;
            }

            TranslationManager tm = GlobalController.Instance.translationManager;
            QuantumRunner runner = QuantumRunner.Default;
            QuantumGame game = QuantumRunner.DefaultGame;

            Activity activity = new();
            if (runner && runner.NetworkClient != null) {
                Room realtimeRoom = runner.NetworkClient.CurrentRoom;

                activity.Party = new() {
                    Size = new() {
                        CurrentSize = realtimeRoom.PlayerCount,
                        MaxSize = realtimeRoom.MaxPlayers,
                    },
                    Id = realtimeRoom.Name + "1",
                };
                activity.State = realtimeRoom.IsVisible ? tm.GetTranslation("discord.public", false) : tm.GetTranslation("discord.private", false);
                activity.Details = tm.GetTranslation("discord.online", false);
                activity.Secrets = new() { Join = realtimeRoom.Name };
            }

            if (game != null) {
                Frame f = game.Frames.Predicted;

                if (f != null && f.Global->GameState >= GameState.Playing) {
                    // In a level
                    if (activity.Details == null) {
                        if (runner.Session.IsReplay) {
                            activity.Details = tm.GetTranslation("discord.replay", false);
                        } else {
                            activity.Details = tm.GetTranslation("discord.offline", false);
                        }
                    }
                    var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
                    var gamemode = f.FindAsset(f.Global->Rules.Gamemode);

                    activity.Assets = new ActivityAssets {
                        LargeImage = !string.IsNullOrWhiteSpace(stage.DiscordStageImage) ? stage.DiscordStageImage : "mainmenu",
                        LargeText = tm.GetTranslation(stage.TranslationKey, false),
                        SmallImage = gamemode.DiscordRpcKey,
                        SmallText = tm.GetTranslation(gamemode.TranslationKey, false),
                    };

                    long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    if (f.Global->Rules.IsTimerEnabled) {
                        activity.Timestamps = new() { End = now + (long) (f.Global->Timer.AsFloat * 1000) };
                    } else {
                        activity.Timestamps = new() { Start = now - ((long) ((f.Number + game.InterpolationFactor) - f.Global->StartFrame) * (1000 / f.UpdateRate)) };
                    }
                }
            } else {
                // In the main menu, not in a room
                activity.Details = tm.GetTranslation("discord.mainmenu", false);
                activity.Assets = new() { LargeImage = "mainmenu" };
            }

            activityManager.UpdateActivity(activity, _ => { });
        }

        private void OnLanguageChanged(TranslationManager tm) {
            UpdateActivity();
        }

        private void OnDiscordIntegrationChanged() {
            UpdateActivity();
        }

        public void TryJoinGame(string secret) {
            Debug.Log($"[Discord] Attempting to join game with secret \"{secret}\"");
            _ = NetworkHandler.JoinRoom(new EnterRoomArgs {
                RoomName = secret,
            });
        }

#pragma warning restore CS0162
#pragma warning restore IDE0079
    }
}
