using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using static NSMB.Utilities.Enums;

namespace NSMB.Utilities {
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
            [PrefabParticleData("Particle/GreenPipe")] Pipe_Break_Green,
            [PrefabParticleData("Particle/GreenPipe-D")] Pipe_Break_Green_Broken,
            [PrefabParticleData("Particle/BluePipe")] Pipe_Break_Blue,
            [PrefabParticleData("Particle/BluePipe-D")] Pipe_Break_Blue_Broken,
            [PrefabParticleData("Particle/RedPipe")] Pipe_Break_Red,
            [PrefabParticleData("Particle/RedPipe-D")] Pipe_Break_Red_Broken,

            [PrefabParticleData("Particle/BulletBillLauncher")] BulletBillLauncher_Break,

            [PrefabParticleData("Particle/Puff")] Enemy_Puff,
            [PrefabParticleData("Particle/EnemyHardKick")] Enemy_HardKick,
            [PrefabParticleData("Particle/KillPoof")] Enemy_KillPoof,

            [PrefabParticleData("Particle/CoinSparkles")] Coin_Sparkles,

            [PrefabParticleData("Particle/WalljumpParticle")] Player_WallJump,
            [PrefabParticleData("Particle/GroundpoundDust")] Player_Groundpound,
            [PrefabParticleData("Particle/MegaGroundpoundStars")] Player_MegaGroundpoundStars,
            [PrefabParticleData("Particle/MegaGroundpoundDust")] Player_MegaGroundpoundDust,
            [PrefabParticleData("Particle/MegaGroundpoundImpact")] Player_MegaGroundpoundImpact,
            [PrefabParticleData("Particle/MegaMushroomGrow")] Player_MegaMushroom,
            [PrefabParticleData("Particle/WaterDust")] Player_WaterDust,
            [PrefabParticleData("Particle/TripleJumpLandingDust")] Player_TripleJumpLandingDust,
            [PrefabParticleData("Particle/MegaFootstep")] Player_MegaFootstep,
            [PrefabParticleData("Particle/PlayerBounce")] Player_PlayerBump,
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
}