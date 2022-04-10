using System.Collections.ObjectModel;
using System.Collections.Generic;
using UnityEngine;

public class Enums {

    public enum PowerupState {
        Mini, Small, Large, FireFlower, IceFlower, PropellerMushroom, Shell, Giant
    }
    // Animation enums
    public enum PlayerEyeState {
        Normal, HalfBlink, FullBlink, Death
    }
    // Music Enums
    public enum MusicState {
        Normal,
        MegaMushroom,
        Starman,
    }
    // Networking Enums
    public static class NetPlayerProperties {
        public static string Character { get; } = "character";
        public static string Ping { get; } = "ping";
        public static string PrimaryColor { get; } = "primarycolor";
        public static string SecondaryColor { get; } = "secondarycolor";
        public static string GameState { get; } = "game_state";
    }
    public static class NetPlayerGameState {
        public static string Stars { get; } = "stars";
        public static string Coins { get; } = "coins";
        public static string Lives { get; } = "lives";
        public static string PowerupState { get; } = "powerup_state";
    }
    public static class NetRoomProperties {
        public static string Level { get; } = "level";
        public static string StarRequirement { get; } = "stars";
        public static string Lives { get; } = "lives";
        public static string Time { get; } = "time";
        public static string NewPowerups { get; } = "newpowerups";
        public static string GameStarted { get; } = "started";
    }
    public enum NetEventIds : byte {
        // 1-9 = in-lobby events
        StartGame = 1,
        ChatMessage = 2,
        // 10-19 = game state events
        PlayerFinishedLoading = 10,
        AllFinishedLoading = 11,
        EndGame = 19,
        // 20-29 = world-based game events
        SetTile = 20,
        BumpTile = 21,
        SetAndBumpTile = 22,
        SetTileBatch = 23,
        ResetTiles = 24,
        // 30-39 = graphical-only events
        SpawnParticle = 30,
        SpawnResizableParticle = 31,
    }
}