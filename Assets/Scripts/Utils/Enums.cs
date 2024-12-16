using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class Enums {
    #region ANIMATION & MUSIC
    //---Animation enums
    public enum PlayerEyeState {
        Normal, HalfBlink, FullBlink, Death
    }

    //---Sound effects
    [Flags]
    public enum SpecialPowerupMusic {
        Starman = 1 << 0,
        MegaMushroom = 1 << 1,
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

        [PrefabParticleData("Prefabs/Particle/EnemyHardKick")] Enemy_HardKick,
        [PrefabParticleData("Prefabs/Particle/KillPoof")] Enemy_KillPoof,

        [PrefabParticleData("Prefabs/Particle/WalljumpParticle")] Player_WallJump,
        [PrefabParticleData("Prefabs/Particle/GroundpoundDust")] Player_Groundpound,
        [PrefabParticleData("Prefabs/Particle/MegaMushroomGrow")] Player_MegaMushroom,
        [PrefabParticleData("Prefabs/Particle/WaterDust")] Player_WaterDust,
    }

    #endregion
    #region NETWORKING
    // Networking Enums
    public static class NetRoomProperties {
        public const string IntProperties = "I";
        public const string BoolProperties = "B";
        public const string HostName = "H";
        public const string StageGuid = "S";
    }
    #endregion
}


public class PrefabParticleData : Attribute {
    public string Path { get; }
    internal PrefabParticleData(string path) {
        Path = path;
    }
}

public static class AttributeExtensions {

    private static readonly Dictionary<SoundEffect, string> CachedStrings = new();
    private static readonly Dictionary<string, AudioClip> CachedClips = new();
    private static readonly Dictionary<Enums.PrefabParticle, GameObject> CachedParticles = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void PreloadSounds() {
        foreach (SoundEffect sound in Enum.GetValues(typeof(SoundEffect))) {
            try {
                sound.GetClip();
            } catch { }
        }
    }

    public static AudioClip GetClip(this SoundEffect soundEffect, CharacterAsset player = null, int variant = 0) {
        string name = "Sound/" + GetClipString(soundEffect) + (variant > 0 ? "_" + variant : "");

        if (player != null) {
            name = name.Replace("{char}", player.SoundFolder);
        }

        if (CachedClips.TryGetValue(name, out AudioClip cachedClip)) {
            return cachedClip;
        }

        AudioClip clip = Resources.Load(name) as AudioClip;
        CachedClips[name] = clip;
        return clip;
    }

    private static string GetClipString(SoundEffect soundEffect) {
        if (CachedStrings.TryGetValue(soundEffect, out string s)) {
            return s;
        }

        // Dirty reflection to get data out of an attribute
        CachedStrings[soundEffect] = soundEffect.GetType().GetMember(soundEffect.ToString())[0].GetCustomAttribute<SoundData>().Sound;
        return CachedStrings[soundEffect];
    }

    public static GameObject GetGameObject(this Enums.PrefabParticle particle) {
        if (CachedParticles.TryGetValue(particle, out GameObject o)) {
            return o;
        }

        // Dirty reflection to get data out of an attribute
        return CachedParticles[particle] = Resources.Load(particle.GetType().GetMember(particle.ToString())[0].GetCustomAttribute<PrefabParticleData>().Path) as GameObject;
    }
}