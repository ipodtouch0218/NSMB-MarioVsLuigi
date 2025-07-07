using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using static Enums;

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
        public const string GamemodeGuid = "G";
    }
    #endregion
}

public class PrefabParticleDataAttribute : Attribute {
    public string Path { get; }
    internal PrefabParticleDataAttribute(string path) {
        Path = path;
    }
}

public static partial class AttributeExtensions {
    private static readonly Dictionary<PrefabParticle, GameObject> CachedParticles = new();

    public static GameObject GetGameObject(this Enums.PrefabParticle particle) {
        if (CachedParticles.TryGetValue(particle, out GameObject o)) {
            return o;
        }

        // Dirty reflection to get data out of an attribute
        return CachedParticles[particle] = Resources.Load(particle.GetType().GetMember(particle.ToString())[0].GetCustomAttribute<PrefabParticleDataAttribute>().Path) as GameObject;
    }
}