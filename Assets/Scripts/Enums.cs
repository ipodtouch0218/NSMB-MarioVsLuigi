using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enums {

    public enum PowerupState {
        Small, Mini, Large, FireFlower, Shell, Giant
    }
    public enum PlayerEyeState {
        Normal, HalfBlink, FullBlink, Death
    }
    // Networking Enums
    public static class NetRoomProperties {
        public static string Level { get; } = "level";
        public static string StarRequirement { get; } = "stars";
    }
    public enum NetEventIds : byte {
        // 1-9 = in-lobby events
        StartGame = 1,
        ChatMessage = 2,
        // 10-19 = game state events
        EndGame = 10,
        PlayerFinishedLoading = 11,
        // 20-29 = world-based game events
        SetTile = 20,
        BumpTile = 21,
        // 30-39 = graphical-only events
        SpawnParticle = 30,

    }
}