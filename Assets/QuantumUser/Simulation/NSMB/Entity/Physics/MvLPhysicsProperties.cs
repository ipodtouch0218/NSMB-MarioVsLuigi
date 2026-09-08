namespace Quantum {
    public class MvLPhysicsProperties : PhysicsMaterial {

#if QUANTUM_UNITY
        [UnityEngine.Header("Mario vs Luigi Properties (above ones do nothing)")]
#endif
        public bool IsSlipperyGround;
        public bool IsSlideableGround;
        public SoundEffect FootstepSound = SoundEffect.Player_Walk_Grass;
        public ParticleEffect FootstepParticle = ParticleEffect.None;

    }
}