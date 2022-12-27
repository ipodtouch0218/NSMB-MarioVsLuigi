
using UnityEngine;
using Fusion;

/// <summary>
/// Interface for <see cref="SpawnPointManagerPrototype{T}"/> behaviours.
/// </summary>
public interface ISpawnPointManagerPrototype<T> where T : Component, ISpawnPointPrototype {
  void CollectSpawnPoints(NetworkRunner runner);
  Transform GetNextSpawnPoint(NetworkRunner runner, PlayerRef player, bool skipIfBlocked = true);
}

