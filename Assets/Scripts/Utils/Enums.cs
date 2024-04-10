using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static class Enums {

    public enum PowerupState : byte {
        NoPowerup,
        MiniMushroom,
        Mushroom,
        FireFlower,
        IceFlower,
        PropellerMushroom,
        BlueShell,
        MegaMushroom
    }

    public static PowerupScriptable GetPowerupScriptable(this PowerupState state) {
        if (state == PowerupState.NoPowerup) {
            return null;
        }

        return ScriptableManager.Instance.powerups.FirstOrDefault(powerup => powerup.state == state);
    }

    public enum GameState : byte {
        Loading = 0,  // Waiting for all players to finish loading the game
        Starting = 1, // All players are loaded, starting animation is playing
        Playing = 2,  // Game is actively playing
        Ended = 3     // Game ended, returning to the room menu soon
    }

    #region ANIMATION & MUSIC
    //---Animation enums
    public enum PlayerEyeState {
        Normal, HalfBlink, FullBlink, Death
    }

    //---Sound effects
    public enum Sounds : byte {
        //CURRENT HIGHEST NUMBER: 103
        //Enemy
        [SoundData("enemy/freeze")]                             Enemy_Generic_Freeze = 0,
        [SoundData("enemy/freeze_shatter")]                     Enemy_Generic_FreezeShatter = 1,
        [SoundData("enemy/stomp")]                              Enemy_Generic_Stomp = 2,
        [SoundData("enemy/boo_laugh")]                          Enemy_Boo_Laugh = 101,
        [SoundData("enemy/bobomb_explode")]                     Enemy_Bobomb_Explode = 3,
        [SoundData("enemy/bobomb_fuse")]                        Enemy_Bobomb_Fuse = 4,
        [SoundData("enemy/bulletbill_shoot")]                   Enemy_BulletBill_Shoot = 5,
        [SoundData("enemy/piranhaplant_chomp")]                 Enemy_PiranhaPlant_Chomp = 7, //HARDCODED: DO NOT CHANGE WITHOUT CHANGING PIRAHNA PLANT ANIMATION FIRST
        [SoundData("enemy/piranhaplant_death")]                 Enemy_PiranhaPlant_Death = 6,
        [SoundData("enemy/shell_kick")]                         Enemy_Shell_Kick = 8,
        [SoundData("enemy/shell_combo1")]                       Enemy_Shell_Combo1 = 9,
        [SoundData("enemy/shell_combo2")]                       Enemy_Shell_Combo2 = 10,
        [SoundData("enemy/shell_combo3")]                       Enemy_Shell_Combo3 = 11,
        [SoundData("enemy/shell_combo4")]                       Enemy_Shell_Combo4 = 12,
        [SoundData("enemy/shell_combo5")]                       Enemy_Shell_Combo5 = 13,
        [SoundData("enemy/shell_combo6")]                       Enemy_Shell_Combo6 = 14,
        [SoundData("enemy/shell_combo7")]                       Enemy_Shell_Combo7 = 15,

        //Player
        [SoundData("player/collision")]                         Player_Sound_Collision = 17,
        [SoundData("player/collision_fireball")]                Player_Sound_Collision_Fireball = 18,
        [SoundData("player/crouch")]                            Player_Sound_Crouch = 19,
        [SoundData("player/death")]                             Player_Sound_Death = 20,
        [SoundData("player/death_others")]                      Player_Sound_DeathOthers = 94,
        [SoundData("player/drill")]                             Player_Sound_Drill = 21,
        [SoundData("player/groundpound_start")]                 Player_Sound_GroundpoundStart = 22,
        [SoundData("player/groundpound_landing")]               Player_Sound_GroundpoundLanding = 23,
        [SoundData("player/jump")]                              Player_Sound_Jump = 24,
        [SoundData("player/lava_hiss")]                         Player_Sound_LavaHiss = 90,
        [SoundData("player/powerup")]                           Player_Sound_PowerupCollect = 16, //HARDCODED: DO NOT CHANGE WITHOUT CHANGING POWERUPS SCRIPTABLES
        [SoundData("player/powerup_reserve_store")]             Player_Sound_PowerupReserveStore = 25,
        [SoundData("player/powerup_reserve_use")]               Player_Sound_PowerupReserveUse = 26,
        [SoundData("player/powerdown")]                         Player_Sound_Powerdown = 27,
        [SoundData("player/respawn")]                           Player_Sound_Respawn = 28,
        [SoundData("player/slide_end")]                         Player_Sound_SlideEnd = 92,
        [SoundData("player/swim")]                              Player_Sound_Swim = 96,
        [SoundData("player/walljump")]                          Player_Sound_WallJump = 29,
        [SoundData("player/wallslide")]                         Player_Sound_WallSlide = 30,

        [SoundData("player/walk/grass")]                        Player_Walk_Grass = 31,
        [SoundData("player/walk/snow")]                         Player_Walk_Snow = 32,
        [SoundData("player/walk/sand")]                         Player_Walk_Sand = 93,
        [SoundData("player/walk/water")]                        Player_Walk_Water = 95,

        [SoundData("character/{char}/doublejump")]              Player_Voice_DoubleJump = 33,
        [SoundData("character/{char}/lava_death")]              Player_Voice_LavaDeath = 34,
        [SoundData("character/{char}/mega_mushroom")]           Player_Voice_MegaMushroom = 35,
        [SoundData("character/{char}/selected")]                Player_Voice_Selected = 36,
        [SoundData("character/{char}/spinner_launch")]          Player_Voice_SpinnerLaunch = 37,
        [SoundData("character/{char}/triplejump")]              Player_Voice_TripleJump = 38,
        [SoundData("character/{char}/walljump")]                Player_Voice_WallJump = 39,
        [SoundData("character/{char}/mega_mushroom_collect")]   Player_Sound_MegaMushroom_Collect = 40, //HARDCODED: DO NOT CHANGE WITHOUT CHANGING POWERUPS SCRIPTABLES

        //Powerup
        [SoundData("powerup/1-up")]                             Powerup_Sound_1UP = 78, //HARDCODED: DO NOT CHANGE WITHOUT CHANGING POWERUPS SCRIPTABLES
        [SoundData("powerup/blueshell_enter")]                  Powerup_BlueShell_Enter = 41,
        [SoundData("powerup/blueshell_slide")]                  Powerup_BlueShell_Slide = 42,
        [SoundData("powerup/fireball_break")]                   Powerup_Fireball_Break = 43,
        [SoundData("powerup/fireball_shoot")]                   Powerup_Fireball_Shoot = 44,
        [SoundData("powerup/iceball_break")]                    Powerup_Iceball_Break = 46,
        [SoundData("powerup/iceball_shoot")]                    Powerup_Iceball_Shoot = 47,
        [SoundData("powerup/megamushroom_break_block")]         Powerup_MegaMushroom_Break_Block = 48,
        [SoundData("powerup/megamushroom_break_pipe")]          Powerup_MegaMushroom_Break_Pipe = 49,
        [SoundData("powerup/megamushroom_end")]                 Powerup_MegaMushroom_End = 50,
        [SoundData("powerup/megamushroom_groundpound")]         Powerup_MegaMushroom_Groundpound = 51,
        [SoundData("powerup/megamushroom_jump")]                Powerup_MegaMushroom_Jump = 52,
        [SoundData("powerup/megamushroom_walk")]                Powerup_MegaMushroom_Walk = 53,
        [SoundData("powerup/minimushroom_collect")]             Powerup_MiniMushroom_Collect = 45, //HARDCODED: DO NOT CHANGE WITHOUT CHANGING POWERUPS SCRIPTABLES
        [SoundData("powerup/minimushroom_groundpound")]         Powerup_MiniMushroom_Groundpound = 54,
        [SoundData("powerup/minimushroom_jump")]                Powerup_MiniMushroom_Jump = 55,
        [SoundData("powerup/minimushroom_waterwalk")]           Powerup_MiniMushroom_WaterWalk = 97,
        [SoundData("powerup/propellermushroom_drill")]          Powerup_PropellerMushroom_Drill = 56,
        [SoundData("powerup/propellermushroom_kick")]           Powerup_PropellerMushroom_Kick = 57,
        [SoundData("powerup/propellermushroom_spin")]           Powerup_PropellerMushroom_Spin = 58,
        [SoundData("powerup/propellermushroom_start")]          Powerup_PropellerMushroom_Start = 59,

        //UI Sounds / Songs / Jingles
        [SoundData("ui/hurry_up")]                              UI_HurryUp = 60,
        [SoundData("ui/loading")]                               UI_Loading = 61,
        [SoundData("ui/match_lose")]                            UI_Match_Lose = 62,
        [SoundData("ui/match_win")]                             UI_Match_Win = 63,
        [SoundData("ui/pause")]                                 UI_Pause = 64,
        [SoundData("ui/quit")]                                  UI_Quit = 65,
        [SoundData("ui/start_game")]                            UI_StartGame = 66,
        [SoundData("ui/player_connect")]                        UI_PlayerConnect = 79,
        [SoundData("ui/player_disconnect")]                     UI_PlayerDisconnect = 80,
        [SoundData("ui/decide")]                                UI_Decide = 81,
        [SoundData("ui/back")]                                  UI_Back = 82,
        [SoundData("ui/cursor")]                                UI_Cursor = 83,
        [SoundData("ui/warn")]                                  UI_Error = 84,
        [SoundData("ui/windowclosed")]                          UI_WindowClose = 85,
        [SoundData("ui/windowopen")]                            UI_WindowOpen = 86,
        [SoundData("ui/match_draw")]                            UI_Match_Draw = 87,
        [SoundData("ui/countdown0")]                            UI_Countdown_0 = 88,
        [SoundData("ui/countdown1")]                            UI_Countdown_1 = 89,
        [SoundData("ui/file_select")]                           UI_FileSelect = 98,
        [SoundData("ui/chat_keyup")]                            UI_Chat_KeyUp = 102,
        [SoundData("ui/chat_keydown")]                          UI_Chat_KeyDown = 103,
        [SoundData("ui/chat_send")]                             UI_Chat_Send = 104,

        //World Elements
        [SoundData("world/block_break")]                        World_Block_Break = 67,
        [SoundData("world/block_bump")]                         World_Block_Bump = 68,
        [SoundData("world/block_powerup")]                      World_Block_Powerup = 69,
        [SoundData("world/block_powerup_mega")]                 World_Block_Powerup_Mega = 99,
        [SoundData("world/coin_collect")]                       World_Coin_Collect = 70,
        [SoundData("world/coin_drop")]                          World_Coin_Drop = 91,
        [SoundData("world/coin_dotted_spawn")]                  World_Coin_Dotted_Spawn = 100,
        [SoundData("world/ice_skidding")]                       World_Ice_Skidding = 71,
        [SoundData("world/spinner_launch")]                     World_Spinner_Launch = 72,
        [SoundData("world/star_collect")]                       World_Star_Collect = 73,
        [SoundData("world/star_collect_enemy")]                 World_Star_CollectOthers = 74,
        [SoundData("world/star_nearby")]                        World_Star_Nearby = 75,
        [SoundData("world/star_spawn")]                         World_Star_Spawn = 76,
        [SoundData("world/water_splash")]                       World_Water_Splash = 77,
    }

    [Flags]
    public enum SpecialPowerupMusic {
        Starman = 1 << 0,
        MegaMushroom = 1 << 1,
    }

    public enum Particle : byte {
        None = 0,

        Entity_BrickBreak = 1,
        Entity_Coin = 2,
        Generic_Puff = 3,
        Walk_Sand = 4,
        Walk_Sand_Right = 5,
        Walk_Snow = 6,
        Walk_Snow_Right = 7,
    }

    public enum PrefabParticle : byte {
        [PrefabParticleData("Prefabs/Particle/GreenPipe")] Pipe_Break_Green,
        [PrefabParticleData("Prefabs/Particle/GreenPipe-D")] Pipe_Break_Green_Broken,
        [PrefabParticleData("Prefabs/Particle/BluePipe")] Pipe_Break_Blue,
        [PrefabParticleData("Prefabs/Particle/BluePipe-D")] Pipe_Break_Blue_Broken,
        [PrefabParticleData("Prefabs/Particle/RedPipe")] Pipe_Break_Red,
        [PrefabParticleData("Prefabs/Particle/RedPipe-D")] Pipe_Break_Red_Broken,

        [PrefabParticleData("Prefabs/Particle/BulletBillLauncher")] BulletBillLauncher_Break,

        [PrefabParticleData("Prefabs/Particle/Puff")] Enemy_Puff,

        [PrefabParticleData("Prefabs/Particle/WalljumpParticle")] Player_WallJump,
    }

    #endregion
    #region NETWORKING
    // Networking Enums
    public static class NetRoomProperties {
        public const string IntProperties = "I";
        public const string BoolProperties = "B";
        public const string HostName = "H";
    }
    #endregion
}

public class SoundData : Attribute {
    public string Sound { get; }
    internal SoundData(string sound) {
        Sound = sound;
    }
}

public class PrefabParticleData : Attribute {
    public string Path { get; }
    internal PrefabParticleData(string path) {
        Path = path;
    }
}

public static class AttributeExtensions {

    private static readonly Dictionary<Enums.Sounds, string> CachedStrings = new();
    private static readonly Dictionary<string, AudioClip> CachedClips = new();
    private static readonly Dictionary<Enums.PrefabParticle, GameObject> CachedParticles = new();

    public static AudioClip GetClip(this Enums.Sounds sound, CharacterData player = null, int variant = 0) {
        string name = "Sound/" + GetClipString(sound) + (variant > 0 ? "_" + variant : "");

        if (player != null) {
            name = name.Replace("{char}", player.soundFolder);
        }

        if (CachedClips.TryGetValue(name, out AudioClip cachedClip)) {
            return cachedClip;
        }

        AudioClip clip = Resources.Load(name) as AudioClip;
        CachedClips[name] = clip;
        return clip;
    }

    private static string GetClipString(Enums.Sounds sound) {
        if (CachedStrings.TryGetValue(sound, out string s)) {
            return s;
        }

        // Dirty reflection to get data out of an attribute
        CachedStrings[sound] = sound.GetType().GetMember(sound.ToString())[0].GetCustomAttribute<SoundData>().Sound;
        return CachedStrings[sound];
    }

    public static GameObject GetGameObject(this Enums.PrefabParticle particle) {
        if (CachedParticles.TryGetValue(particle, out GameObject o)) {
            return o;
        }

        // Dirty reflection to get data out of an attribute
        return CachedParticles[particle] = Resources.Load(particle.GetType().GetMember(particle.ToString())[0].GetCustomAttribute<PrefabParticleData>().Path) as GameObject;
    }
}
