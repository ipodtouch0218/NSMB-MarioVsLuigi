using UnityEngine;
using Fusion;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
using Fusion.Editor;
#endif

/// <summary>
/// Derive from this class for different <see cref="ISpawnPointPrototype"/> types. 
/// Derived manager will only find that spawn point type, allowing for separate handling of player spawn points from other spawn-able items such as AI.
/// </summary>
/// <typeparam name="T"></typeparam>
[ScriptHelp(BackColor = EditorHeaderBackColor.Steel)]
public abstract class SpawnPointManagerPrototype<T> : Fusion.Behaviour, ISpawnPointManagerPrototype<T>
  where T : Component, ISpawnPointPrototype {
  public enum SpawnSequence {
    PlayerId,
    RoundRobin,
    Random
  }

  /// <summary>
  /// How spawn points will be selected from the <see cref="_spawnPoints"/> collection.
  /// </summary>
  [InlineHelp]
  public SpawnSequence Sequence;

  /// <summary>
  /// LayerMask for which physics layers should be used for blocked spawn point checks.
  /// </summary>
  [InlineHelp]
  public LayerMask BlockingLayers;

  /// <summary>
  /// The search radius used for detecting if a spawn point is blocked by an object.
  /// </summary>
  [InlineHelp]
  public float BlockedCheckRadius = 2f;

  /// <summary>
  /// Serialized collection of all <see cref="ISpawnPointPrototype"/> of the type T found in the same scene as this component.
  /// </summary>
  [System.NonSerialized]
  internal List<Component> _spawnPoints = new List<Component>();

  [System.NonSerialized]
  public int LastSpawnIndex = -1;

  NetworkRNG rng;

  private void Awake() {
    rng = new NetworkRNG(0);
  }

#if UNITY_EDITOR
  [BehaviourAction]
  protected void DrawFoundSpawnPointCount() {
    if (Application.isPlaying == false) {
      GUILayout.BeginVertical(FusionGUIStyles.GroupBoxType.Info.GetStyle());
      GUILayout.Space(4);
      if (GUI.Button(EditorGUILayout.GetControlRect(), "Find Spawn Points")) {
        _spawnPoints.Clear();
        var found = UnityEngine.SceneManagement.SceneManager.GetActiveScene().FindObjectsOfTypeInOrder<T, Component>();
        _spawnPoints.AddRange(found);
      }
      GUILayout.Space(4);

      EditorGUI.BeginDisabledGroup(true);
      foreach (var point in _spawnPoints) {
        EditorGUILayout.ObjectField(point.name, point, typeof(T),  true);
      }
      EditorGUI.EndDisabledGroup();

      EditorGUILayout.LabelField($"{typeof(T).Name}(s): {_spawnPoints.Count}");
      GUILayout.EndVertical();
    }
  }
#endif


  /// <summary>
  /// Find all <see cref="ISpawnPointPrototype"/> instances in the same scene as this spawner. 
  /// This should only be done at development time if using the Photon relay for any spawn logic.
  /// </summary>
  public void CollectSpawnPoints(NetworkRunner runner) {
    _spawnPoints.Clear();
    _spawnPoints.AddRange(runner.SimulationUnityScene.FindObjectsOfTypeInOrder<T, Component>());
  }

  /// <summary>
  /// Select the next spawn point using the defined <see cref="Sequence"/>. Override this method to expand on this, such as detecting if a spawn point is blocked.
  /// </summary>
  public virtual Transform GetNextSpawnPoint(NetworkRunner runner, PlayerRef player, bool skipIfBlocked = true) {

    CollectSpawnPoints(runner);

    int spawnCount = _spawnPoints.Count;

    if (_spawnPoints == null || spawnCount == 0)
      return null;

    Component next;
    int nextIndex;
    if (Sequence == SpawnSequence.PlayerId) {
      nextIndex = player % spawnCount;
      next = _spawnPoints[nextIndex];
    }
    else if (Sequence == SpawnSequence.RoundRobin) {
      nextIndex = (LastSpawnIndex + 1) % spawnCount;
      next = _spawnPoints[nextIndex];
    } else {
      nextIndex = rng.RangeInclusive(0, spawnCount);
      next = _spawnPoints[nextIndex];
    }
    
    // Handling for blocked spawn points. By default this never happens, as the IsBlocked test always returns true.
    if (skipIfBlocked && BlockingLayers.value != 0 && IsBlocked(next)) {
      var unblocked = GetNextUnblocked(nextIndex);
      if (unblocked.Item1 > -1) {
        LastSpawnIndex = unblocked.Item1;
        return unblocked.Item2.transform;
      }
      // leave LastSpawnIndex the same since we haven't arrived at a new spawn point.
      next = unblocked.Item2;
    } else {
      LastSpawnIndex = nextIndex;
      return next.transform;
    }
    return AllSpawnPointsBlockedFallback();
  }

  /// <summary>
  /// Handling for if all spawn points are blocked.
  /// </summary>
  /// <returns></returns>
  public virtual Transform AllSpawnPointsBlockedFallback() {
    return transform;
  }

  /// <summary>
  /// Cycles through all remaining spawn points searching for unblocked. Will return null if all points return <see cref="IsBlocked(Transform)"/> == true.
  /// </summary>
  /// <param name="failedIndex">The index of the first tried SpawnPoints[] element, which was blocked.</param>
  /// <returns>(<see cref="_spawnPoints"/> index, <see cref="ISpawnPointPrototype"/>)</returns>
  public virtual (int, Component) GetNextUnblocked(int failedIndex) {
    for (int i = 1, cnt = _spawnPoints.Count; i < cnt; ++i) {
      var sp = _spawnPoints[i % cnt];
      if (!IsBlocked(sp))
        return (i, sp);
    }
    return (-1, null);
  }

  protected static Collider[] blocked3D;
  /// <summary>
  /// Override this method with any logic for checking if a spawn point is blocked.
  /// </summary>
  /// <param name="spawnPoint"></param>
  /// <returns></returns>
  public virtual bool IsBlocked(Component spawnPoint) {
    var physics3d = spawnPoint.gameObject.scene.GetPhysicsScene();
    if (physics3d != null) {
      if (blocked3D == null) {
        blocked3D = new Collider[1];
      }
      var blockedCount = physics3d.OverlapSphere(spawnPoint.transform.position, BlockedCheckRadius, blocked3D, BlockingLayers.value, QueryTriggerInteraction.UseGlobal);
      if (blockedCount > 0)
        Debug.LogWarning(blocked3D[0].name + " is blocking " + spawnPoint.name);

      return blockedCount > 0;
    }

    var physics2d = spawnPoint.gameObject.scene.GetPhysicsScene2D();

    if (physics2d != null) {

      throw new System.NotImplementedException();
      //return false;
    }

    return false;
  }

}

