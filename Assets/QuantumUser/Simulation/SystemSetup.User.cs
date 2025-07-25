using Quantum.Core;
using System.Collections.Generic;

namespace Quantum {
    public static partial class DeterministicSystemSetup {
        static partial void AddSystemsUser(ICollection<SystemBase> systems, RuntimeConfig gameConfig, SimulationConfig simulationConfig, SystemsConfig systemsConfig) {
            // The system collection is already filled with systems coming from the SystemsConfig.
            // Add or remove systems to the collection: systems.Add(new SystemFoo());

            // Remove the default systems
            systems.Clear();

            systems.Add(new EntityPrototypeSystem());
            systems.Add(new PlayerConnectedSystem());
            systems.Add(new MvLCullingSystem());
            systems.Add(new GameLogicSystem());
            systems.Add(
                new StartDisabledSystemGroup("gameplay",
                    new PrePhysicsObjectSystem(),
                    new InteractionPhysicsQuerySystem(),
                    new GenericMoverSystem(),
                    new SpinnerSystem(),
                    new MovingPlatformPhysicsQuerySystem(),
                    new PhysicsSystem2D(),
                    new MovingPlatformSystem(),
                    new EnemySystem(),
                    new InteractionSystem(),
                    new PhysicsObjectSystem(),
                    new GoombaSystem(),
                    new KoopaSystem(),
                    new BobombSystem(),
                    new PiranhaPlantSystem(),
                    new BulletBillSystem(),
                    new BooSystem(),
                    new ProjectileSystem(),
                    new CoinItemSystem(),
                    new PowerupSystem(),
                    new BlockBumpSystem(),
                    new MarioPlayerSystem(),
                    new CoinSystem(),
                    new GoldBlockSystem(),
                    new WrappingObjectSystem(),
                    new BigStarSystem(),
                    new ObjectiveCoinSystem(),
                    new HoldableObjectSystem(),
                    new IceBlockSystem(),
                    new CameraSystem(),
                    new LiquidSystem(),
                    new BreakableObjectSystem(),
                    new MarioBrosPlatformSystem(),
                    new EnterablePipeSystem()
                    // new MiniscriptSystem()
                    // new BetterPhysicsObjectSystem()
                )
            );
            systems.Add(new StageSystem());

#if MVL_DEBUG
            // This HAS to be the last system otherwise it breaks replays.
            systems.Add(new MvLDebugSystem());
#endif
        }
    }
}