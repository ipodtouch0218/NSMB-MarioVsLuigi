using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public enum SoundEffect : int {
    //CURRENT HIGHEST NUMBER: 132 (use 133 next)
    //Enemy
    [SoundEffectData("{style}/enemy/freeze")] Enemy_Generic_Freeze = 0,
    [SoundEffectData("{style}/enemy/freeze_shatter")] Enemy_Generic_FreezeShatter = 1,
    [SoundEffectData("{style}/enemy/freeze_melt")] Enemy_Generic_FreezeMelt = 105,
    [SoundEffectData("{style}/enemy/stomp")] Enemy_Generic_Stomp = 2,
    [SoundEffectData("{style}/enemy/boo_laugh")] Enemy_Boo_Laugh = 101,
    [SoundEffectData("{style}/enemy/bobomb_explode")] Enemy_Bobomb_Explode = 3,
    [SoundEffectData("{style}/enemy/bobomb_fuse")] Enemy_Bobomb_Fuse = 4,
    [SoundEffectData("{style}/enemy/bulletbill_shoot")] Enemy_BulletBill_Shoot = 5,
    [SoundEffectData("{style}/enemy/piranhaplant_chomp")] Enemy_PiranhaPlant_Chomp = 7, //HARDCODED: DO NOT CHANGE WITHOUT CHANGING PIRAHNA PLANT ANIMATION FIRST
    [SoundEffectData("{style}/enemy/piranhaplant_death")] Enemy_PiranhaPlant_Death = 6,
    [SoundEffectData("{style}/enemy/shell_kick")] Enemy_Shell_Kick = 8,
    [SoundEffectData("{style}/enemy/shell_combo1")] Enemy_Shell_Combo1 = 9,
    [SoundEffectData("{style}/enemy/shell_combo2")] Enemy_Shell_Combo2 = 10,
    [SoundEffectData("{style}/enemy/shell_combo3")] Enemy_Shell_Combo3 = 11,
    [SoundEffectData("{style}/enemy/shell_combo4")] Enemy_Shell_Combo4 = 12,
    [SoundEffectData("{style}/enemy/shell_combo5")] Enemy_Shell_Combo5 = 13,
    [SoundEffectData("{style}/enemy/shell_combo6")] Enemy_Shell_Combo6 = 14,
    [SoundEffectData("{style}/enemy/shell_combo7")] Enemy_Shell_Combo7 = 15,

    //Player
    [SoundEffectData("{style}/player/collision")] Player_Sound_Collision = 17,
    [SoundEffectData("{style}/player/collision_fireball")] Player_Sound_Collision_Fireball = 18,
    [SoundEffectData("{style}/player/crouch")] Player_Sound_Crouch = 19,
    [SoundEffectData("{style}/player/death")] Player_Sound_Death = 20,
    [SoundEffectData("{style}/player/death_others")] Player_Sound_DeathOthers = 94,
    [SoundEffectData("{style}/player/drill")] Player_Sound_Drill = 21,
    [SoundEffectData("{style}/player/groundpound_start")] Player_Sound_GroundpoundStart = 22,
    [SoundEffectData("{style}/player/groundpound_landing")] Player_Sound_GroundpoundLanding = 23,
    [SoundEffectData("{style}/player/lava_hiss")] Player_Sound_LavaHiss = 90,
    [SoundEffectData("{style}/player/powerup")] Player_Sound_PowerupCollect = 16, //HARDCODED: DO NOT CHANGE WITHOUT CHANGING POWERUPS SCRIPTABLES
    [SoundEffectData("{style}/player/powerup_reserve_store")] Player_Sound_PowerupReserveStore = 25,
    [SoundEffectData("{style}/player/powerup_reserve_use")] Player_Sound_PowerupReserveUse = 26,
    [SoundEffectData("{style}/player/powerdown")] Player_Sound_Powerdown = 27,
    [SoundEffectData("{style}/player/respawn")] Player_Sound_Respawn = 28,
    [SoundEffectData("{style}/player/slide_end")] Player_Sound_SlideEnd = 92,
    [SoundEffectData("{style}/player/swim")] Player_Sound_Swim = 96,
    [SoundEffectData("{style}/player/walljump")] Player_Sound_WallJump = 29,
    [SoundEffectData("{style}/player/wallslide")] Player_Sound_WallSlide = 30,
    [SoundEffectData("{style}/player/pipe")] Player_Sound_PipeEnter = 119,
    [SoundEffectData("{style}/player/throw")] Player_Sound_Throw = 129,
    [SoundEffectData("{style}/player/pickup")] Player_Sound_PickUp = 130,

    [SoundEffectData("{style}/player/walk/grass", 2)] Player_Walk_Grass = 31,
    [SoundEffectData("{style}/player/walk/snow", 2)] Player_Walk_Snow = 32,
    [SoundEffectData("{style}/player/walk/sand", 2)] Player_Walk_Sand = 93,
    [SoundEffectData("{style}/player/walk/water", 2)] Player_Walk_Water = 95,

    [SoundEffectData("{style}/character/{char}/doublejump", 2)] Player_Voice_DoubleJump = 33,
    [SoundEffectData("{style}/character/{char}/lava_death")] Player_Voice_LavaDeath = 34,
    [SoundEffectData("{style}/character/{char}/mega_mushroom")] Player_Voice_MegaMushroom = 35,
    [SoundEffectData("{style}/character/{char}/selected")] Player_Voice_Selected = 36,
    [SoundEffectData("{style}/character/{char}/spinner_launch")] Player_Voice_SpinnerLaunch = 37,
    [SoundEffectData("{style}/character/{char}/triplejump")] Player_Voice_TripleJump = 38,
    [SoundEffectData("{style}/character/{char}/walljump", 2)] Player_Voice_WallJump = 39,
    [SoundEffectData("{style}/character/{char}/mega_mushroom_collect")] Player_Sound_MegaMushroom_Collect = 40, //HARDCODED: DO NOT CHANGE WITHOUT CHANGING POWERUPS SCRIPTABLES
    [SoundEffectData("{style}/character/{char}/jump")] Player_Sound_Jump = 24,
    [SoundEffectData("{style}/character/{char}/jumpsmall")] Player_Sound_JumpSmall = 118,
    [SoundEffectData("{style}/character/{char}/powerup")] Player_Voice_Powerup = 125,
    [SoundEffectData("{style}/character/{char}/reserve")] Player_Voice_Reserve = 126,
    [SoundEffectData("{style}/character/{char}/1up")] Player_Voice_1Up = 127,
    [SoundEffectData("{style}/character/{char}/powerdown")] Player_Voice_Powerdown = 128,
    [SoundEffectData("{style}/character/{char}/death")] Player_Voice_Death = 131,
    [SoundEffectData("{style}/character/{char}/hahano")] Player_Voice_HahaNo = 132,

    //Powerup
    [SoundEffectData("{style}/powerup/1-up")] Powerup_1UP_Collect = 78, //HARDCODED: DO NOT CHANGE WITHOUT CHANGING POWERUPS SCRIPTABLES
    [SoundEffectData("{style}/powerup/blueshell_enter")] Powerup_BlueShell_Enter = 41,
    [SoundEffectData("{style}/powerup/blueshell_slide")] Powerup_BlueShell_Slide = 42,
    [SoundEffectData("{style}/powerup/bomb_throw")] Powerup_Bomberman_Throw = 124,
    [SoundEffectData("{style}/powerup/fireball_break")] Powerup_Fireball_Break = 43,
    [SoundEffectData("{style}/powerup/fireball_shoot")] Powerup_Fireball_Shoot = 44,
    [SoundEffectData("{style}/powerup/iceball_break")] Powerup_Iceball_Break = 46,
    [SoundEffectData("{style}/powerup/iceball_shoot")] Powerup_Iceball_Shoot = 47,
    [SoundEffectData("{style}/powerup/lightningball_shoot")] Powerup_Lightning_Shoot = 115,
    [SoundEffectData("{style}/powerup/lightningball_break")] Powerup_Lightning_Break = 116,
    [SoundEffectData("{style}/powerup/lightningball_charge")] Powerup_Lightning_Charge = 117,
    [SoundEffectData("{style}/powerup/magmaball_shoot")] Powerup_Magmaball_Shoot = 120,
    [SoundEffectData("{style}/powerup/magmaball_break")] Powerup_Magmaball_Break = 121,
    [SoundEffectData("{style}/powerup/magmaball_burn")] Powerup_Magmaball_Burn = 122,
    [SoundEffectData("{style}/powerup/cape_spin")] Powerup_Cape_Spin = 123,
    [SoundEffectData("{style}/powerup/megamushroom_break_block")] Powerup_MegaMushroom_Break_Block = 48,
    [SoundEffectData("{style}/powerup/megamushroom_break_pipe")] Powerup_MegaMushroom_Break_Pipe = 49,
    [SoundEffectData("{style}/powerup/megamushroom_end")] Powerup_MegaMushroom_End = 50,
    [SoundEffectData("{style}/powerup/megamushroom_groundpound")] Powerup_MegaMushroom_Groundpound = 51,
    [SoundEffectData("{style}/powerup/megamushroom_jump")] Powerup_MegaMushroom_Jump = 52,
    [SoundEffectData("{style}/powerup/megamushroom_walk", 2)] Powerup_MegaMushroom_Walk = 53,
    [SoundEffectData("{style}/powerup/minimushroom_collect")] Powerup_MiniMushroom_Collect = 45, //HARDCODED: DO NOT CHANGE WITHOUT CHANGING POWERUPS SCRIPTABLES
    [SoundEffectData("{style}/powerup/minimushroom_groundpound")] Powerup_MiniMushroom_Groundpound = 54,
    [SoundEffectData("{style}/powerup/minimushroom_jump")] Powerup_MiniMushroom_Jump = 55,
    [SoundEffectData("{style}/powerup/minimushroom_waterwalk")] Powerup_MiniMushroom_WaterWalk = 97,
    [SoundEffectData("{style}/powerup/propellermushroom_drill")] Powerup_PropellerMushroom_Drill = 56,
    [SoundEffectData("{style}/powerup/propellermushroom_kick")] Powerup_PropellerMushroom_Kick = 57,
    [SoundEffectData("{style}/powerup/propellermushroom_spin")] Powerup_PropellerMushroom_Spin = 58,
    [SoundEffectData("{style}/powerup/propellermushroom_start")] Powerup_PropellerMushroom_Start = 59,
    [SoundEffectData("{style}/powerup/hammer_throw")] Powerup_HammerSuit_Throw = 106, //HARDCODED: DO NOT CHANGE WITHOUT CHANGING POWERUPS SCRIPTABLES
    [SoundEffectData("{style}/powerup/hammersuit_bounce")] Powerup_HammerSuit_Bounce = 109,
    [SoundEffectData("{style}/powerup/hahano")] Powerup_HahaNo = 133,

    //UI Sounds / Songs / Jingles
    [SoundEffectData("{style}/world/hurry_up")] UI_HurryUp = 60,
    [SoundEffectData("ui/loading")] UI_Loading = 61,
    [SoundEffectData("{style}/world/match_lose")] UI_Match_Lose = 62,
    [SoundEffectData("{style}/world/match_win")] UI_Match_Win = 63,
    [SoundEffectData("{style}/world/match_draw")] UI_Match_Draw = 87,
    [SoundEffectData("{style}/world/match_cancel")] UI_Match_Cancel = 107,
    [SoundEffectData("{style}/world/pause")] UI_Pause = 64,
    [SoundEffectData("ui/quit")] UI_Quit = 65,
    [SoundEffectData("{style}/world/start_game")] UI_StartGame = 66,
    [SoundEffectData("ui/player_connect")] UI_PlayerConnect = 79,
    [SoundEffectData("ui/player_disconnect")] UI_PlayerDisconnect = 80,
    [SoundEffectData("ui/decide")] UI_Decide = 81,
    [SoundEffectData("ui/back")] UI_Back = 82,
    [SoundEffectData("ui/cursor")] UI_Cursor = 83,
    [SoundEffectData("ui/warn")] UI_Error = 84,
    [SoundEffectData("ui/windowclosed")] UI_WindowClose = 85,
    [SoundEffectData("ui/windowopen")] UI_WindowOpen = 86,
    [SoundEffectData("ui/countdown0")] UI_Countdown_0 = 88,
    [SoundEffectData("ui/countdown1")] UI_Countdown_1 = 89,
    [SoundEffectData("ui/file_select")] UI_FileSelect = 98,
    [SoundEffectData("ui/chat_keyup")] UI_Chat_KeyUp = 102,
    [SoundEffectData("ui/chat_keydown")] UI_Chat_KeyDown = 103,
    [SoundEffectData("ui/chat_fulltype")] UI_Chat_FullType = 108,
    [SoundEffectData("ui/chat_send")] UI_Chat_Send = 104,

    //World Elements
    [SoundEffectData("{style}/world/block_break")] World_Block_Break = 67,
    [SoundEffectData("{style}/world/block_bump")] World_Block_Bump = 68,
    [SoundEffectData("{style}/world/block_powerup")] World_Block_Powerup = 69,
    [SoundEffectData("{style}/world/block_powerup_mega")] World_Block_Powerup_Mega = 99,
    [SoundEffectData("{style}/world/coin_collect")] World_Coin_Collect = 70,
    [SoundEffectData("{style}/world/coin_drop")] World_Coin_Drop = 91,
    [SoundEffectData("{style}/world/coin_dotted_spawn")] World_Coin_Dotted_Spawn = 100,
    [SoundEffectData("{style}/world/ice_skidding")] World_Ice_Skidding = 71,
    [SoundEffectData("{style}/world/gold_block_damage")] World_Gold_Block_Damage = 112,
    [SoundEffectData("{style}/world/gold_block_equip")] World_Gold_Block_Equip = 113,
    [SoundEffectData("{style}/world/gold_block_finished")] World_Gold_Block_Finished = 114,
    [SoundEffectData("{style}/world/spinner_launch")] World_Spinner_Launch = 72,
    [SoundEffectData("{style}/world/starcoin_collect")] World_Starcoin_Collect = 110,
    [SoundEffectData("{style}/world/starcoin_store")] World_Starcoin_Store = 111,
    [SoundEffectData("{style}/world/star_collect")] World_Star_Collect = 73,
    [SoundEffectData("{style}/world/star_collect_enemy")] World_Star_CollectOthers = 74,
    [SoundEffectData("{style}/world/star_nearby")] World_Star_Nearby = 75,
    [SoundEffectData("{style}/world/star_spawn")] World_Star_Spawn = 76,
    [SoundEffectData("{style}/world/water_splash")] World_Water_Splash = 77,

}

public class SoundEffectDataAttribute : Attribute {
    public string Sound { get; }
    public int Variants { get; }
    internal SoundEffectDataAttribute(string sound, int variants = 1) {
        Sound = sound;
        Variants = variants;
    }
}

public static partial class AttributeExtensions {

    private static readonly Dictionary<SoundEffect, SoundEffectDataAttribute> CachedDatas = new();
    private static readonly Dictionary<string, AudioClip> CachedClips = new();

    public static AudioClip GetClip(this SoundEffect soundEffect, StageTheme gameStyle, CharacterAsset player = null, int variant = 0) {
        if (!CachedDatas.TryGetValue(soundEffect, out SoundEffectDataAttribute data)) {
            data = CachedDatas[soundEffect] = soundEffect.GetSoundData();
        }

        return data.GetClip(gameStyle, player, variant);
    }

    public static AudioClip GetClip(this SoundEffectDataAttribute data, StageTheme gameStyle, CharacterAsset player = null, int variant = 0) {
        string name = "Sound/" + data.Sound + (variant > 0 ? "_" + variant : "");

        if (player != null) {
            name = name.Replace("{char}", player.SoundFolder);
        }

        name = name.Replace("{style}", gameStyle.ToString());

        if (CachedClips.TryGetValue(name, out AudioClip cachedClip)) {
            return cachedClip;
        }

        AudioClip clip = Resources.Load(name) as AudioClip;
        CachedClips[name] = clip;
        return clip;
    }

    public static SoundEffectDataAttribute GetSoundData(this SoundEffect soundEffect) {
        // Dirty reflection to get data out of an attribute
        return soundEffect.GetType().GetMember(soundEffect.ToString())[0].GetCustomAttribute<SoundEffectDataAttribute>();
    }
}