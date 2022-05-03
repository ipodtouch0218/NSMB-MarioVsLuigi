using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;
using Discord;

#pragma warning disable CS0162 // Unreachable code, but it isnt unreachable
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
    }

    public void TryJoinGame(string secret) {
        if (SceneManager.GetActiveScene().buildIndex != 0)
            return;

        string[] split = secret.Split("-");
        string region = split[0];
        string room = split[1];

        MainMenuManager.lastRegion = region;
        MainMenuManager.Instance.connectThroughSecret = room;
        PhotonNetwork.Disconnect();
    }

    public void AskToJoin(ref User user) {
        activityManager.SendRequestReply(user.Id, ActivityJoinRequestReply.Yes, (res) => {
            Debug.Log(res);
        });
    }

    public void Update() {
#if UNITY_WEBGL
        return;
#endif

        discord.RunCallbacks();
    }

    public void OnDisable() {
        discord.Dispose();
    }

    public void UpdateActivity() {
#if UNITY_WEBGL
        return;
#endif

        Activity activity = new();

        if (GameManager.Instance) {
            //in a level
            GameManager gm = GameManager.Instance;
            Room room = PhotonNetwork.CurrentRoom;

            activity.Details = PhotonNetwork.OfflineMode ? "Playing Offline" : "Playing Online";
            activity.Party = new() { Size = new() { CurrentSize = room.PlayerCount, MaxSize = room.MaxPlayers }, Id = PhotonNetwork.CurrentRoom.Name };
            activity.State = ((string) room.CustomProperties[Enums.NetRoomProperties.Password]) == "" ? "In a Public Lobby" : "In a Private Lobby";
            activity.Secrets = new() { Join = PhotonNetwork.CloudRegion + "-" + room.Name };

            if (gm.richPresenceId != "")
                activity.Assets = new() { LargeImage = $"level-{gm.richPresenceId}" };

            if (gm.timedGameDuration == -1) {
                activity.Timestamps = new() { Start = gm.startRealTime / 1000 };
            } else {
                activity.Timestamps = new() { End = gm.endRealTime / 1000 };
            }

        } else if (PhotonNetwork.InRoom) {
            //in a room
            Room room = PhotonNetwork.CurrentRoom;

            string pw = (string) room.CustomProperties[Enums.NetRoomProperties.Password];

            activity.Details = PhotonNetwork.OfflineMode ? "Playing Offline" : "Playing Online";
            activity.Party = new() { Size = new() { CurrentSize = room.PlayerCount, MaxSize = room.MaxPlayers }, Id = PhotonNetwork.CurrentRoom.Name };
            activity.State = pw == "" ? "In a Public Lobby" : "In a Private Lobby";
            activity.Secrets = new() { Join = PhotonNetwork.CloudRegion + "-" + room.Name };

            activity.Assets = new() { LargeImage = "mainmenu" };

        } else {
            //in the main menu, not in a room

            activity.Details = "Browsing the Main Menu...";
            activity.Assets = new() { LargeImage = "mainmenu" };

        }


        activityManager.UpdateActivity(activity, (res) => {
            //head empty.
            Debug.Log($"Discord activity update: {res}");
        });
    }
}