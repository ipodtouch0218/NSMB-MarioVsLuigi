using System.Collections.ObjectModel;
using System.Collections.Generic;
using UnityEngine;

public class Enums {

    public enum PowerupState {
        Mini, Small, Large, FireFlower, PropellerMushroom, Shell, Giant
    }
    public enum PlayerEyeState {
        Normal, HalfBlink, FullBlink, Death
    }
    // Networking Enums
    public static class NetPlayerProperties {
        public static string Character { get; } = "character";
        public static string Ping { get; } = "ping";
    }
    public static class NetRoomProperties {
        public static string Level { get; } = "level";
        public static string StarRequirement { get; } = "stars";
        public static string Lives { get; } = "lives";
        public static string NewPowerups { get; } = "newpowerups";
    }
    public enum NetEventIds : byte {
        // 1-9 = in-lobby events
        StartGame = 1,
        ChatMessage = 2,
        // 10-19 = game state events
        PlayerFinishedLoading = 10,
        SetGameStartTimestamp = 11,
        EndGame = 19,
        // 20-29 = world-based game events
        SetTile = 20,
        BumpTile = 21,
        SetTileBatch = 22,
        ResetTiles = 23,
        // 30-39 = graphical-only events
        SpawnParticle = 30,
        SpawnResizableParticle = 31,
    }
    //TODO: event caching?
    // private static List<byte> uncached = new List<byte>(new byte[]{1, 2, 10, 11, 21, 30, 31});
    // public static ReadOnlyCollection<byte> uncachedEvents => uncached.AsReadOnly();
    public enum MusicState {
        Normal,
        MegaMushroom,
        Starman,
    }
}