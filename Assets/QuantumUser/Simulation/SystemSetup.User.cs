using System.Collections.Generic;
namespace Quantum {
    public static partial class DeterministicSystemSetup {
        static partial void AddSystemsUser(ICollection<SystemBase> systems, RuntimeConfig gameConfig, SimulationConfig simulationConfig, SystemsConfig systemsConfig) {
            // The system collection is already filled with systems coming from the SystemsConfig.
            // Add or remove systems to the collection: systems.Add(new SystemFoo());

            systems.Add(new StageSystem());
            systems.Add(new MarioPlayerSystem());
            systems.Add(new BigStarSystem());
            systems.Add(new ProjectileSystem());
            systems.Add(new PowerupSystem());
            systems.Add(new LiquidSystem());
            systems.Add(new PhysicsObjectSystem());
            systems.Add(new CoinSystem());
            systems.Add(new WrappingObjectSystem());
            systems.Add(new CameraSystem());
        }
    }
}