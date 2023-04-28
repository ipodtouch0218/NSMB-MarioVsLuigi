using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

using Discord;
using Fusion;
using NSMB.Utils;
using NSMB.Translation;

public class DiscordController : MonoBehaviour {

    //---Static Variables
    private static readonly long DiscordAppId = 962073502469459999;

    //---Properties
    private TranslationManager Translation => GlobalController.Instance.translationManager;

    //---Private Variables
    private Discord.Discord discord;
    private ActivityManager activityManager;

    public void OnEnable() {
        GlobalController.Instance.translationManager.OnLanguageChanged += OnLanguageChanged;
    }

    public void OnDisable() {
        GlobalController.Instance.translationManager.OnLanguageChanged -= OnLanguageChanged;
        discord?.Dispose();
    }

    private void OnLanguageChanged(TranslationManager tm) {
        if (NetworkHandler.Runner) {
            UpdateActivity(NetworkHandler.Runner.SessionInfo);
        } else {
            UpdateActivity(null);
        }
    }

    public void Start() {
#if UNITY_WEBGL
        enabled = false;
        return;
#endif

        discord = new Discord.Discord(DiscordAppId, (ulong) CreateFlags.NoRequireDiscord);
        activityManager = discord.GetActivityManager();
        activityManager.OnActivityJoinRequest += AskToJoin;
        activityManager.OnActivityJoin += TryJoinGame;

        try {
            string filename = AppDomain.CurrentDomain.ToString();
            filename = string.Join(" ", filename.Split(" ")[..^2]);
            string dir = AppDomain.CurrentDomain.BaseDirectory + Path.DirectorySeparatorChar + filename;
            activityManager.RegisterCommand(dir);
            Debug.Log($"[DISCORD] Set launch path to \"{dir}\"");
        } catch {
            Debug.Log($"[DISCORD] Failed to set launch path (on {Application.platform})");
        }
    }

    public void TryJoinGame(string secret) {
        //TODO: MainMenu jank...
        if (SceneManager.GetActiveScene().buildIndex != 0)
            return;

        Debug.Log($"[DISCORD] Attempting to join game with secret \"{secret}\"");

        //TODO: add "disconnect" prompt
        _ = NetworkHandler.JoinRoom(secret);
    }

    //TODO this doesn't work???
    public void AskToJoin(ref User user) {
        //activityManager.SendRequestReply(user.Id, ActivityJoinRequestReply.Yes, (res) => {
        //    Debug.Log($"[DISCORD] Ask to Join response: {res}");
        //});
    }

    public void Update() {
        try {
            discord.RunCallbacks();
        } catch { }
    }

    public void UpdateActivity(SessionInfo session = null) {
#if UNITY_WEBGL
        return;
#endif
        if (discord == null || activityManager == null || !Application.isPlaying)
            return;

        Activity activity = new();
        session ??= NetworkHandler.Runner?.SessionInfo;

        if (SessionData.Instance) {

            activity.Details = NetworkHandler.Runner.IsSinglePlayer ? Translation.GetTranslation("discord.offline") : Translation.GetTranslation("discord.online");
            if (!NetworkHandler.Runner.IsSinglePlayer) {
                Utils.GetSessionProperty(session, Enums.NetRoomProperties.MaxPlayers, out int maxSize);
                activity.Party = new() { Size = new() { CurrentSize = session.PlayerCount, MaxSize = maxSize }, Id = session.Name + "1" };
            }
            activity.State = session.IsVisible ? Translation.GetTranslation("discord.public") : Translation.GetTranslation("discord.private");
            activity.Secrets = new() { Join = session.Name };

            if (GameManager.Instance) {
                //in a level
                GameManager gm = GameManager.Instance;

                ActivityAssets assets = new();
                if (gm.richPresenceId != "")
                    assets.LargeImage = "level-" + gm.richPresenceId;
                else
                    assets.LargeImage = "mainmenu";
                assets.LargeText = gm.levelName;

                activity.Assets = assets;

                if (SessionData.Instance.Timer <= 0) {
                    activity.Timestamps = new() { Start = (long) gm.gameStartTimestamp };
                } else {
                    activity.Timestamps = new() { End = (long) gm.gameEndTimestamp };
                }
            } else {
                //in a room, but on the main menu.
                activity.Assets = new() { LargeImage = "mainmenu" };
            }
        } else {
            //in the main menu, not in a room
            activity.Details = Translation.GetTranslation("discord.mainmenu");
            activity.Assets = new() { LargeImage = "mainmenu" };
        }

        activityManager.UpdateActivity(activity, (res) => {
            //head empty.
            Debug.Log($"[Discord] Rich Presence Update: {res}");
        });
    }
}
