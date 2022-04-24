using System.Collections.ObjectModel;
using System.Collections.Generic;
using UnityEngine;

public class Enums {

    public class PriorityPair {
        public int itemPriority, statePriority;
        public PriorityPair(int state) {
            statePriority = itemPriority = state;
        }
        public PriorityPair(int state, int item) {
            statePriority = state;
            itemPriority = item;
        }
    }
    public static readonly Dictionary<PowerupState, PriorityPair> PowerupStatePriority = new() {
        [PowerupState.None] = new(-1),
        [PowerupState.MiniMushroom] = new(0, 3),
        [PowerupState.Small] = new(-1),
        [PowerupState.Large] = new(1),
        [PowerupState.FireFlower] = new(2),
        [PowerupState.IceFlower] = new(2),
        [PowerupState.PropellerMushroom] = new(2),
        [PowerupState.BlueShell] = new(2),
        [PowerupState.MegaMushroom] = new(4),
    };
    public enum PowerupState {
        None, MiniMushroom, Small, Large, FireFlower, IceFlower, PropellerMushroom, BlueShell, MegaMushroom
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
        public static string Character { get; } = "C";
        public static string Ping { get; } = "P";
        public static string PrimaryColor { get; } = "C1";
        public static string SecondaryColor { get; } = "C2";
        public static string GameState { get; } = "GS";
    }
    public static class NetPlayerGameState {
        public static string Stars { get; } = "S";
        public static string Coins { get; } = "C";
        public static string Lives { get; } = "L";
        public static string PowerupState { get; } = "PS";
    }
    public static class NetRoomProperties {
        public static string Level { get; } = "L";
        public static string StarRequirement { get; } = "SR";
        public static string Lives { get; } = "Li";
        public static string Time { get; } = "T";
        public static string NewPowerups { get; } = "C";
        public static string GameStarted { get; } = "S";
        public static string HostName { get; } = "HN";
        public static string Password { get; } = "PW";
        public static string DiscordJoinSecret { get; } = "DJS";
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
        SyncTilemap = 25,
        // 30-39 = graphical-only events
        SpawnParticle = 30,
        SpawnResizableParticle = 31,
    }
}