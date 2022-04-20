using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;
using Discord;

public class DiscordController : MonoBehaviour {

    public Discord.Discord discord;
    public ActivityManager activityManager;

    public void Awake() {
        discord = new Discord.Discord(962073502469459999, (ulong) CreateFlags.NoRequireDiscord);
        activityManager = discord.GetActivityManager();
        activityManager.OnActivityJoin += TryJoinGame;
        activityManager.OnActivityJoinRequest += AskToJoin;
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
        activityManager.AcceptInvite(user.Id, (res) => {
            Debug.Log(res);
        });
    }

    public void Update() {
        discord.RunCallbacks();
    }

    public void OnDisable() {
        discord.Dispose();
    }

    public void UpdateActivity() {

        Activity activity = new();

        if (GameManager.Instance) {
            //in a level
            GameManager gm = GameManager.Instance;
            Room room = PhotonNetwork.CurrentRoom;

            activity.Details = PhotonNetwork.OfflineMode ? "Playing Offline" : "Playing Online";
            activity.Party = new() { Size = new() { CurrentSize = room.PlayerCount, MaxSize = room.MaxPlayers }, Id = PhotonNetwork.CurrentRoom.Name };
            activity.State = ((string) room.CustomProperties[Enums.NetRoomProperties.Password]) == "" ? "In a Public Lobby" : "In a Private Lobby";

            if (gm.richPresenceId != "")
                activity.Assets = new() { LargeImage = $"level-{gm.richPresenceId}" };
            if (gm.timedGameDuration == -1) {
                activity.Timestamps = new() { Start = gm.startTime };
            } else {
                activity.Timestamps = new() { Start = gm.timedGameDuration };
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
            Debug.Log(res);
        });
    }

}