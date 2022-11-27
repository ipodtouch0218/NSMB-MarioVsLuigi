using System;
using UnityEngine;
using UnityEngine.SceneManagement;

using Discord;
using Fusion;

public class DiscordController : MonoBehaviour {

    public Discord.Discord discord;
    public ActivityManager activityManager;

    public void Awake() {
#if UNITY_WEBGL
        return;
#endif

        discord = new Discord.Discord(962073502469459999, (ulong) CreateFlags.NoRequireDiscord);
        activityManager = discord.GetActivityManager();
        activityManager.OnActivityJoinRequest += AskToJoin;
        activityManager.OnActivityJoin += TryJoinGame;

//#if UNITY_STANDALONE_WIN
        try {
            string filename = AppDomain.CurrentDomain.ToString();
            filename = string.Join(" ", filename.Split(" ")[..^2]);
            string dir = AppDomain.CurrentDomain.BaseDirectory + "\\" + filename;
            activityManager.RegisterCommand(dir);
            Debug.Log($"[DISCORD] Set launch path to \"{dir}\"");
        } catch {
            Debug.Log($"[DISCORD] Failed to set launch path (on {Application.platform})");
        }
//#endif
    }

    public void TryJoinGame(string secret) {
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
#if UNITY_WEBGL
        return;
#endif
        try {
            discord.RunCallbacks();
        } catch { }
    }

    public void OnDisable() {
        discord?.Dispose();
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

            activity.Details = NetworkHandler.Runner.IsSinglePlayer ? "Playing Offline" : "Playing Online";
            activity.Party = new() { Size = new() { CurrentSize = session.PlayerCount + 1, MaxSize = SessionData.Instance.MaxPlayers }, Id = session.Name + "1" };
            activity.State = session.IsVisible ? "In a Public Game" : "In a Private Game";
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
                    activity.Timestamps = new() { Start = gm.gameStartTimestamp / 1000 };
                } else {
                    activity.Timestamps = new() { End = gm.gameEndTimestamp / 1000 };
                }

            } else {
                //in a room, but on the main menu.
                activity.Assets = new() { LargeImage = "mainmenu" };
            }

        } else {
            //in the main menu, not in a room
            activity.Details = "Browsing the Main Menu...";
            activity.Assets = new() { LargeImage = "mainmenu" };
        }


        activityManager.UpdateActivity(activity, (res) => {
            //head empty.
            Debug.Log($"[DISCORD] Rich Presence Update: {res}");
        });
    }
}