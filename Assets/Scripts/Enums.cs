using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;

public static class Enums {

    #region POWERUPS
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
    #endregion
    #region ANIMATION & MUSIC
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
    //Sound effects
    public enum Sounds : byte {

        //Enemy
        [SoundData("enemy/freeze")]                     Enemy_Generic_Freeze,
        [SoundData("enemy/freeze_shatter")]             Enemy_Generic_FreezeShatter,
        [SoundData("enemy/kick")]                       Enemy_Generic_Kick,
        [SoundData("enemy/stomp")]                      Enemy_Generic_Stomp,
        [SoundData("enemy/bobomb_explode")]             Enemy_Bobomb_Explode,
        [SoundData("enemy/bobomb_fuse")]                Enemy_Bobomb_Fuse,
        [SoundData("enemy/bulletbill_shoot")]           Enemy_BulletBill_Shoot,
        [SoundData("enemy/piranhaplant_chomp")]         Enemy_PiranhaPlant_Chomp = 7,
        [SoundData("enemy/piranhaplant_death")]         Enemy_PiranhaPlant_Death,

        //Player
        [SoundData("player/collision")]                 Player_Sound_Collision,
        [SoundData("player/crouch")]                    Player_Sound_Crouch,
        [SoundData("player/death")]                     Player_Sound_Death,
        [SoundData("player/drill")]                     Player_Sound_Drill,
        [SoundData("player/groundpound_start")]         Player_Sound_GroundpoundStart,
        [SoundData("player/groundpound_landing")]       Player_Sound_GroundpoundLanding,
        [SoundData("player/jump")]                      Player_Sound_Jump,
        [SoundData("player/powerup")]                   Player_Sound_PowerupCollect,
        [SoundData("player/powerup_reserve_store")]     Player_Sound_PowerupReserveStore,
        [SoundData("player/powerup_reserve_use")]       Player_Sound_PowerupReserveUse,
        [SoundData("player/powerdown")]                 Player_Sound_Powerdown,
        [SoundData("player/respawn")]                   Player_Sound_Respawn,
        [SoundData("player/walljump")]                  Player_Sound_WallJump,
        [SoundData("player/wallslide")]                 Player_Sound_WallSlide,

        [SoundData("player/walk/grass")]                Player_Walk_Grass,
        [SoundData("player/walk/snow")]                 Player_Walk_Snow,

        [SoundData("character/{char}/doublejump")]      Player_Voice_DoubleJump,
        [SoundData("character/{char}/lava_death")]      Player_Voice_LavaDeath,
        [SoundData("character/{char}/mega_mushroom")]   Player_Voice_MegaMushroom,
        [SoundData("character/{char}/selected")]        Player_Voice_Selected,
        [SoundData("character/{char}/spinner_launch")]  Player_Voice_SpinnerLaunch,
        [SoundData("character/{char}/triplejump")]      Player_Voice_TripleJump,
        [SoundData("character/{char}/walljump")]        Player_Voice_WallJump,

        //Powerup
        [SoundData("powerup/blueshell_enter")]          Powerup_BlueShell_Enter,
        [SoundData("powerup/blueshell_slide")]          Powerup_BlueShell_Slide,
        [SoundData("powerup/fireball_break")]           Powerup_Fireball_Break,
        [SoundData("powerup/fireball_shoot")]           Powerup_Fireball_Shoot,
        [SoundData("powerup/iceball_break")]            Powerup_Iceball_Break,
        [SoundData("powerup/iceball_shoot")]            Powerup_Iceball_Shoot,
        [SoundData("powerup/megamushroom_collect")]     Powerup_MegaMushroom_Collect,
        [SoundData("powerup/megamushroom_end")]         Powerup_MegaMushroom_End,
        [SoundData("powerup/megamushroom_groundpound")] Powerup_MegaMushroom_Groundpound,
        [SoundData("powerup/megamushroom_jump")]        Powerup_MegaMushroom_Jump,
        [SoundData("powerup/minimushroom_collect")]     Powerup_MiniMushroom_Collect,
        [SoundData("powerup/minimushroom_groundpound")] Powerup_MiniMushroom_Groundpound,
        [SoundData("powerup/minimushroom_jump")]        Powerup_MiniMushroom_Jump,
        [SoundData("powerup/propellermushroom_drill")]  Powerup_PropellerMushroom_Drill,
        [SoundData("powerup/propellermushroom_kick")]   Powerup_PropellerMushroom_Kick,
        [SoundData("powerup/propellermushroom_spin")]   Powerup_PropellerMushroom_Spin,
        [SoundData("powerup/propellermushroom_start")]  Powerup_PropellerMushroom_Start,

        //UI Sounds / Songs / Jingles
        [SoundData("ui/hurry_up")]                      UI_HurryUp,
        [SoundData("ui/loading")]                       UI_Loading,
        [SoundData("ui/match_lose")]                    UI_Match_Lose,
        [SoundData("ui/match_win")]                     UI_Match_Win,
        [SoundData("ui/pause")]                         UI_Pause,
        [SoundData("ui/quit")]                          UI_Quit,
        [SoundData("ui/start_game")]                    UI_StartGame,

        //World Elements
        [SoundData("world/block_break")]                World_Block_Break,
        [SoundData("world/block_bump")]                 World_Block_Bump,
        [SoundData("world/block_powerup")]              World_Block_Powerup,
        [SoundData("world/coin_collect")]               World_Coin_Collect,
        [SoundData("world/ice_skidding")]               World_Ice_Skidding,
        [SoundData("world/spinner_launch")]             World_Spinner_Launch,
        [SoundData("world/star_collect")]               World_Star_Collect,
        [SoundData("world/star_nearby")]                World_Star_Nearby,
        [SoundData("world/water_splash")]               World_Water_Splash,
    }

    #endregion
    #region NETWORKING 
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
    #endregion
}

public class SoundData : Attribute {
    public string Sound { get; private set; }
    internal SoundData(string sound) {
        Sound = sound;
    }
}
public static class SoundDataExtensions {
    public static AudioClip GetClip(this Enums.Sounds sound, PlayerData player = null, int variant = 0) {
        SoundData data = GetSoundDataFromSound(sound);
        string name = "Sound/" + data.Sound + (variant > 0 ? "_" + variant : "");
        if (player != null)
            name = name.Replace("{char}", player.soundFolder);
        return Resources.Load(name) as AudioClip;
    }
    private static SoundData GetSoundDataFromSound(Enums.Sounds sound) {
        return sound.GetType().GetMember(sound.ToString())[0].GetCustomAttribute<SoundData>();
    }
}