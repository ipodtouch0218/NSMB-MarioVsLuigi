using System;
using System.Collections.Generic;
using System.Reflection;
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
        [PowerupState.Mushroom] = new(1),
        [PowerupState.FireFlower] = new(2),
        [PowerupState.IceFlower] = new(2),
        [PowerupState.PropellerMushroom] = new(2),
        [PowerupState.BlueShell] = new(2),
        [PowerupState.MegaMushroom] = new(4),
    };
    public enum PowerupState : byte {
        None, MiniMushroom, Small, Mushroom, FireFlower, IceFlower, PropellerMushroom, BlueShell, MegaMushroom
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
        [SoundData("enemy/freeze")]                     Enemy_Generic_Freeze = 0,
        [SoundData("enemy/freeze_shatter")]             Enemy_Generic_FreezeShatter = 1,
        [SoundData("enemy/kick")]                       Enemy_Generic_Kick = 2,
        [SoundData("enemy/stomp")]                      Enemy_Generic_Stomp = 3,
        [SoundData("enemy/bobomb_explode")]             Enemy_Bobomb_Explode = 4,
        [SoundData("enemy/bobomb_fuse")]                Enemy_Bobomb_Fuse = 5,
        [SoundData("enemy/bulletbill_shoot")]           Enemy_BulletBill_Shoot = 6,
        [SoundData("enemy/piranhaplant_chomp")]         Enemy_PiranhaPlant_Chomp = 7, //HARDCODED: DO NOT CHANGE WITHOUT CHANGING PIRAHNA PLANT ANIMATION FIRST
        [SoundData("enemy/piranhaplant_death")]         Enemy_PiranhaPlant_Death = 8,

        //Player
        [SoundData("player/collision")]                 Player_Sound_Collision = 9,
        [SoundData("player/collision_fireball")]        Player_Sound_Collision_Fireball = 10,
        [SoundData("player/crouch")]                    Player_Sound_Crouch = 11,
        [SoundData("player/death")]                     Player_Sound_Death = 12,
        [SoundData("player/1-up")]                      Player_Sound_Life = 13, //HARDCODED: DO NOT CHANGE WITHOUT CHANGING POWERUPS SCRIPTABLES
        [SoundData("player/drill")]                     Player_Sound_Drill = 14,
        [SoundData("player/groundpound_start")]         Player_Sound_GroundpoundStart = 15,
        [SoundData("player/groundpound_landing")]       Player_Sound_GroundpoundLanding = 16,
        [SoundData("player/jump")]                      Player_Sound_Jump = 17,
        [SoundData("player/powerup")]                   Player_Sound_PowerupCollect = 18, //HARDCODED: DO NOT CHANGE WITHOUT CHANGING POWERUPS SCRIPTABLES
        [SoundData("player/powerup_reserve_store")]     Player_Sound_PowerupReserveStore = 19,
        [SoundData("player/powerup_reserve_use")]       Player_Sound_PowerupReserveUse = 20,
        [SoundData("player/powerdown")]                 Player_Sound_Powerdown = 21,
        [SoundData("player/respawn")]                   Player_Sound_Respawn = 22,
        [SoundData("player/walljump")]                  Player_Sound_WallJump = 23,
        [SoundData("player/wallslide")]                 Player_Sound_WallSlide = 24,

        [SoundData("player/walk/grass")]                Player_Walk_Grass = 25,
        [SoundData("player/walk/snow")]                 Player_Walk_Snow = 26,

        [SoundData("character/{char}/doublejump")]      Player_Voice_DoubleJump = 27,
        [SoundData("character/{char}/lava_death")]      Player_Voice_LavaDeath = 28,
        [SoundData("character/{char}/mega_mushroom")]   Player_Voice_MegaMushroom = 29,
        [SoundData("character/{char}/selected")]        Player_Voice_Selected = 30,
        [SoundData("character/{char}/spinner_launch")]  Player_Voice_SpinnerLaunch = 31,
        [SoundData("character/{char}/triplejump")]      Player_Voice_TripleJump = 32,
        [SoundData("character/{char}/walljump")]        Player_Voice_WallJump = 33,

        //Powerup
        [SoundData("powerup/blueshell_enter")]          Powerup_BlueShell_Enter = 34,
        [SoundData("powerup/blueshell_slide")]          Powerup_BlueShell_Slide = 35,
        [SoundData("powerup/fireball_break")]           Powerup_Fireball_Break = 36,
        [SoundData("powerup/fireball_shoot")]           Powerup_Fireball_Shoot = 37,
        [SoundData("powerup/iceball_break")]            Powerup_Iceball_Break = 38,
        [SoundData("powerup/iceball_shoot")]            Powerup_Iceball_Shoot = 39,
        [SoundData("powerup/megamushroom_break_block")] Powerup_MegaMushroom_Break_Block = 40,
        [SoundData("powerup/megamushroom_break_pipe")]  Powerup_MegaMushroom_Break_Pipe = 41,
        [SoundData("powerup/megamushroom_collect")]     Powerup_MegaMushroom_Collect = 42, //HARDCODED: DO NOT CHANGE WITHOUT CHANGING POWERUPS SCRIPTABLES
        [SoundData("powerup/megamushroom_end")]         Powerup_MegaMushroom_End = 43,
        [SoundData("powerup/megamushroom_groundpound")] Powerup_MegaMushroom_Groundpound = 44,
        [SoundData("powerup/megamushroom_jump")]        Powerup_MegaMushroom_Jump = 45,
        [SoundData("powerup/megamushroom_walk")]        Powerup_MegaMushroom_Walk = 46,
        [SoundData("powerup/minimushroom_collect")]     Powerup_MiniMushroom_Collect = 47, //HARDCODED: DO NOT CHANGE WITHOUT CHANGING POWERUPS SCRIPTABLES
        [SoundData("powerup/minimushroom_groundpound")] Powerup_MiniMushroom_Groundpound = 48,
        [SoundData("powerup/minimushroom_jump")]        Powerup_MiniMushroom_Jump = 49,
        [SoundData("powerup/propellermushroom_drill")]  Powerup_PropellerMushroom_Drill = 50,
        [SoundData("powerup/propellermushroom_kick")]   Powerup_PropellerMushroom_Kick = 51,
        [SoundData("powerup/propellermushroom_spin")]   Powerup_PropellerMushroom_Spin = 52,
        [SoundData("powerup/propellermushroom_start")]  Powerup_PropellerMushroom_Start = 53,

        //UI Sounds / Songs / Jingles
        [SoundData("ui/hurry_up")]                      UI_HurryUp = 54,
        [SoundData("ui/loading")]                       UI_Loading = 55,
        [SoundData("ui/match_lose")]                    UI_Match_Lose = 56,
        [SoundData("ui/match_win")]                     UI_Match_Win = 57,
        [SoundData("ui/pause")]                         UI_Pause = 58,
        [SoundData("ui/quit")]                          UI_Quit = 59,
        [SoundData("ui/start_game")]                    UI_StartGame = 60,

        //World Elements
        [SoundData("world/block_break")]                World_Block_Break = 61,
        [SoundData("world/block_bump")]                 World_Block_Bump = 62,
        [SoundData("world/block_powerup")]              World_Block_Powerup = 63,
        [SoundData("world/coin_collect")]               World_Coin_Collect = 64,
        [SoundData("world/ice_skidding")]               World_Ice_Skidding = 65,
        [SoundData("world/spinner_launch")]             World_Spinner_Launch = 66,
        [SoundData("world/star_collect")]               World_Star_Collect_Self = 67,
        [SoundData("world/star_collect_enemy")]         World_Star_Collect_Enemy = 68,
        [SoundData("world/star_nearby")]                World_Star_Nearby = 69, //nice
        [SoundData("world/star_spawn")]                 World_Star_Spawn = 70,
        [SoundData("world/water_splash")]               World_Water_Splash = 71,
    }

    #endregion
    #region NETWORKING 
    // Networking Enums
    public static class NetPlayerProperties {
        public static string Character { get; } = "C";
        public static string Ping { get; } = "P";
        public static string PlayerColor { get; } = "C1";
        public static string GameState { get; } = "S";
        public static string Status { get; } = "St";
    }
    public static class NetPlayerGameState {
        public static string Stars { get; } = "S";
        public static string Coins { get; } = "C";
        public static string Lives { get; } = "L";
        public static string PowerupState { get; } = "P";
        public static string ReserveItem { get; } = "R";
    }
    public static class NetRoomProperties {
        public static string Level { get; } = "L";
        public static string StarRequirement { get; } = "S";
        public static string Lives { get; } = "Li";
        public static string Time { get; } = "T";
        public static string NewPowerups { get; } = "C";
        public static string GameStarted { get; } = "G";
        public static string HostName { get; } = "H";
        public static string Debug { get; } = "D";
    }
    public enum NetEventIds : byte {
        // 1-9 = in-lobby events
        StartGame = 1,
        ChatMessage = 2,
        ChangeMaxPlayers = 3,
        ChangePrivate = 4,
        // 10-19 = game state events
        PlayerFinishedLoading = 10,
        AllFinishedLoading = 11,
        EndGame = 19,
        // 20-29 = world-based game events
        SetTile = 20,
        BumpTile = 21,
        SetThenBumpTile = 22,
        SetTileBatch = 23,
        ResetTiles = 24,
        SyncTilemap = 25,
        SetCoinState = 26,
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