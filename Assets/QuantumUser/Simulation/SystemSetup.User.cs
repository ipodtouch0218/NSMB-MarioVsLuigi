using Quantum.Core;
using System.Collections.Generic;

namespace Quantum {
    public static partial class DeterministicSystemSetup {
        static partial void AddSystemsUser(ICollection<SystemBase> systems, RuntimeConfig gameConfig, SimulationConfig simulationConfig, SystemsConfig systemsConfig) {
            // The system collection is already filled with systems coming from the SystemsConfig.
            // Add or remove systems to the collection: systems.Add(new SystemFoo());

            systems.Add(new PlayerConnectedSystem());
            systems.Add(new ClientSystem());
            systems.Add(new GameLogicSystem());
            systems.Add(
                new GameplaySystemGroup(
                    new EnemySystem(),
                    new GoombaSystem(),
                    new KoopaSystem(),
                    new MarioPlayerSystem(),
                    new ProjectileSystem(),
                    new PowerupSystem(),
                    new BlockBumpSystem(),
                    new PhysicsObjectSystem(),
                    new CoinSystem(),
                    new WrappingObjectSystem(),
                    new BigStarSystem(),
                    new HoldableObjectSystem(),
                    new CameraSystem()
                )
            );
            // Signals only... can't be in a group.
            systems.Add(new StageSystem());
            systems.Add(new LiquidSystem());
        }
    }
}