using System;
using System.IO;
using UnityEngine;

using Discord;
using NSMB.Translation;
using Quantum;
using Photon.Realtime;

public unsafe class DiscordController : MonoBehaviour {
#pragma warning disable IDE0079
#pragma warning disable CS0162

    //---Static Variables
    private static readonly long DiscordAppId = 962073502469459999;

    //---Private Variables
    private Discord.Discord discord;
    private ActivityManager activityManager;

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

        discord = new Discord.Discord(DiscordAppId, (ulong) CreateFlags.NoRequireDiscord);
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
            return;
        }

        try {
            discord.RunCallbacks();
        } catch {
            // Ignored
        }
    }

    public void UpdateActivity(RoomInfo room = null) {
#if UNITY_WEBGL || UNITY_WSA
        return;
#endif
        if (!Application.isPlaying || discord == null) {
            return;
        }

        if (!Settings.Instance.GeneralDiscordIntegration) {
            activityManager.ClearActivity(res => { Debug.Log(res); });
            return;
        }

        TranslationManager tm = GlobalController.Instance.translationManager;
        QuantumRunner runner = QuantumRunner.Default;
        QuantumGame game = QuantumRunner.DefaultGame;
        if (room == null && runner && runner.NetworkClient != null) {
            room = runner.NetworkClient.CurrentRoom;
        }

        Activity activity = new();
        if (room != null) {
            activity.Party = new() {
                Size = new() {
                    CurrentSize = room.PlayerCount,
                    MaxSize = room.MaxPlayers,
                },
                Id = room.Name + "1",
            };
            activity.State = room.IsVisible ? tm.GetTranslation("discord.public") : tm.GetTranslation("discord.private");
            activity.Details = tm.GetTranslation("discord.online");
            activity.Secrets = new() { Join = room.Name };
        }
        if (game != null) {
            // In a level
            activity.Details ??= tm.GetTranslation("discord.offline");
            Frame f = game.Frames.Predicted;
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);

            activity.Assets = new ActivityAssets {
                LargeImage = !string.IsNullOrWhiteSpace(stage.DiscordStageImage) ? stage.DiscordStageImage : "mainmenu",
                LargeText = tm.GetTranslation(stage.TranslationKey).Replace("<sprite name=room_customlevel>", "")
            };

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (game.Configurations.Runtime.TimerEnabled) {
                activity.Timestamps = new() { End = now + (f.Global->Timer * 1000).AsLong };
            } else {
                activity.Timestamps = new() { Start = now - (f.Number * f.DeltaTime * 1000).AsLong };
            }
        } else {
            // In the main menu, not in a room
            activity.Details = tm.GetTranslation("discord.mainmenu");
            activity.Assets = new() { LargeImage = "mainmenu" };
        }

        activityManager.UpdateActivity(activity, (res) => { });
    }

    private void OnLanguageChanged(TranslationManager tm) {
        UpdateActivity(null);
    }

    public void TryJoinGame(string secret) {
        /* 
        // TODO: MainMenu jank...
        if (GameManager.Instance) {
            return;
        }

        Debug.Log($"[Discord] Attempting to join game with secret \"{secret}\"");

        // TODO: add "disconnect" prompt if we're already in a game.
        _ = NetworkHandler.JoinRoom(secret);
        */
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