namespace Quantum {
  using System.Collections.Generic;
  using Photon.Deterministic;

  public static partial class DeterministicCommandSetup {
    static partial void AddCommandFactoriesUser(ICollection<IDeterministicCommandFactory> factories, RuntimeConfig gameConfig, SimulationConfig simulationConfig) {
      // Add or remove commands to the collection.
      // factories.Add(new NavMeshAgentTestSystem.RunTest());
    }
  }
}