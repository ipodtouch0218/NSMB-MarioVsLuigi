using Discord;
using NSMB.Translation;
using Photon.Realtime;
using Quantum;
using System;
using System.IO;
using UnityEngine;

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
    }

    public void OnDisable() {
        TranslationManager.OnLanguageChanged -= OnLanguageChanged;
        discord?.Dispose();
    }

    public void Start() {
#if UNITY_WEBGL || UNITY_WSA
        enabled = false;
#endif

        Initialize();
    }


    private bool Initialize() {
#if UNITY_WEBGL || UNITY_WSA
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
        //activityManager.OnActivityJoinRequest += AskToJoin;
        activityManager.OnActivityJoin += TryJoinGame;

        try {
            string filename = AppDomain.CurrentDomain.ToString();
            filename = string.Join(" ", filename.Split(" ")[..^2]);
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
            activityManager.ClearActivity(res => { });
            return;
        }

        TranslationManager tm = GlobalController.Instance.translationManager;
        QuantumRunner runner = QuantumRunner.Default;
        QuantumGame game = QuantumRunner.DefaultGame;

        Room realtimeRoom = null;
        if (runner && runner.NetworkClient != null) {
            realtimeRoom = runner.NetworkClient.CurrentRoom;
        }

        Activity activity = new();
        if (realtimeRoom != null) {
            activity.Party = new() {
                Size = new() {
                    CurrentSize = realtimeRoom.PlayerCount,
                    MaxSize = realtimeRoom.MaxPlayers,
                },
                Id = realtimeRoom.Name + "1",
            };
            activity.State = realtimeRoom.IsVisible ? tm.GetTranslation("discord.public") : tm.GetTranslation("discord.private");
            activity.Details = tm.GetTranslation("discord.online");
            activity.Secrets = new() { Join = realtimeRoom.Name };
        }
        if (game != null) {
            Frame f = game.Frames.Predicted;

            if (f != null && f.Global->GameState >= GameState.Playing) {
                // In a level
                if (activity.Details == null) {
                    if (runner.Session.IsReplay) {
                        activity.Details = tm.GetTranslation("discord.replay");
                    } else {
                        activity.Details = tm.GetTranslation("discord.offline");
                    }
                }
                var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);

                activity.Assets = new ActivityAssets {
                    LargeImage = !string.IsNullOrWhiteSpace(stage.DiscordStageImage) ? stage.DiscordStageImage : "mainmenu",
                    LargeText = tm.GetTranslation(stage.TranslationKey)
                };

                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (f.Global->Rules.TimerSeconds > 0) {
                    activity.Timestamps = new() { End = now + (f.Global->Timer * 1000).AsLong };
                } else {
                    activity.Timestamps = new() { Start = now - ((f.Number - f.Global->StartFrame) * f.DeltaTime * 1000).AsLong };
                }
            }
        } else {
            // In the main menu, not in a room
            activity.Details = tm.GetTranslation("discord.mainmenu");
            activity.Assets = new() { LargeImage = "mainmenu" };
        }

        activityManager.UpdateActivity(activity, (res) => { });
    }

    private void OnLanguageChanged(TranslationManager tm) {
        UpdateActivity();
    }

    public void TryJoinGame(string secret) {
        // TODO: test
        Debug.Log($"[Discord] Attempting to join game with secret \"{secret}\"");
        _ = NetworkHandler.JoinRoom(new EnterRoomArgs {
            RoomName = secret,
        });
    }

    //TODO this doesn't work???
    public void AskToJoin(ref User user) {
        //activityManager.SendRequestReply(user.Id, ActivityJoinRequestReply.Yes, (res) => {
        //    Debug.Log($"[Discord] Ask to Join response: {res}");
        //});
    }
#pragma warning restore CS0162
#pragma warning restore IDE0079
}