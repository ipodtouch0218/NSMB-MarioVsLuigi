// Removed May 21 2021 (obsoleted months prior)

//using System.Collections.Generic;
//using UnityEngine;
//using Fusion;

///// <summary>
///// Flags a GameObject that should not be active in all runners while running in Shared Instance mode. 
///// Use to disable redundant non-simulation items such as lights or UI while testing. This will SetActive(false) all but one copy of this object.
///// </summary>
//[DisallowMultipleComponent]
//[AddComponentMenu("Fusion/Prototyping/Common Scene Object")]
//[System.Obsolete("Use " + nameof(RunnerVisibilityNode))]
//public class CommonSceneObject : Fusion.Behaviour {

//  public const string FUSION_HELP_TEXT = "Flags this GameObject as object that should only be enabled in one runner in InstanceModes.SharedInstance. " +
//    "While running in Shared Instance mode this will SetActive(false) all but one networked instance. Use to disable objects like lights or UI elements.";

//  public enum PreferredRunner { Server, NotServer, Client }

//  private static Dictionary<int, CommonSceneObject> _actives = new Dictionary<int, CommonSceneObject>();
//  private static Dictionary<int, Queue<CommonSceneObject>> _inactives = new Dictionary<int, Queue<CommonSceneObject>>();

//  [SerializeField]
//  [Tooltip("Will SetActive(false) all copies of this networked GameObject, except one with a " + nameof(NetworkRunner) + " matching this setting.")]
//  private PreferredRunner _preferredRunner = PreferredRunner.Server;

//  [SerializeField]
//  [EditorDisabled]
//  private int _uid;

//  private NetworkRunner _cachedrunner;
//  private NetworkRunner Runner {
//    get {
//      if (_cachedrunner == null)
//        _cachedrunner = FindRunner();
//      return _cachedrunner;
//    }
//  }
//  private bool _preferredRunnerFound;

//  private void Reset() {
//    _uid = Random.Range(0, int.MaxValue);
//  }

//  public void Awake() {

//    if (NetworkProjectConfigAsset.Instance.Config.PeerMode != NetworkProjectConfig.PeerModes.Multiple) {
//      enabled = false;
//      return;
//    }

//    // if an instance of this uid is already active, inactive any others.
//    if (_actives.ContainsKey(_uid)) {

//      if (!_inactives.ContainsKey(_uid))
//        _inactives.Add(_uid, new Queue<CommonSceneObject>());

//      _inactives[_uid].Enqueue(this);

//      gameObject.SetActive(false);

//    } else {
//      _actives.Add(_uid, this);
//    }
//  }

//  private void OnApplicationQuit() {
//    _inactives.Clear();
//  }

//  private void OnDestroy() {
//    _actives.Remove(_uid);
//    TrySwitchActiveToNextInstance();
//  }

//  public void Update() {
//    if (_preferredRunnerFound)
//      return;

//    SearchForPreferredRunner();
//  }

//  private void TrySwitchActiveToNextInstance() {
//    if (_inactives.TryGetValue(_uid, out var commons)) {

//      if (commons.Count > 0) {
//        var newactive = commons.Dequeue();
//        _actives.Add(_uid, newactive);
//        if (newactive != null)
//          newactive.gameObject.SetActive(true);
//      }
//    }
//  }

//  private void SearchForPreferredRunner() {
//    var runner = Runner;
//    var pref = _preferredRunner;

//    // Test if current active object already is the preferred runner.
//    if (runner != null) {
//      if (
//        (!runner.IsServer && pref == PreferredRunner.NotServer) ||
//        (runner.IsServer && pref == PreferredRunner.Server) ||
//        (runner.IsClient && pref == PreferredRunner.Client)) {
//        _preferredRunnerFound = true;
//        return;
//      }
//    }

//    if (_inactives.TryGetValue(_uid, out var inactives)) {

//      // cycle through all inactive until we find a server runner
//      for (int i = 0, cnt = inactives.Count; i < cnt; ++i) {
//        var other = inactives.Dequeue();
//        var otherRunner = other.Runner;
//        if (otherRunner == null) {
//          inactives.Enqueue(other);
//          continue;
//        }

//        var isServer = otherRunner.IsServer;
//        var isClient = otherRunner.IsClient;
//        // if we found an alternate instance that is our desired runner type, switch that to active
//        if (
//          (!isServer && pref == PreferredRunner.NotServer) ||
//          (isServer  && pref == PreferredRunner.Server) ||
//          (isClient  && pref == PreferredRunner.Client) ) {

//          this.gameObject.SetActive(false);
//          other.gameObject.SetActive(true);
//          _actives[_uid] = other;
//          other._preferredRunnerFound = true;
//          inactives.Enqueue(this);
//          return;
//        }

//        // Return to the front of the queue if it didn't meet our requirements.
//        inactives.Enqueue(other);
//      }
//    }
//  }

//  private NetworkRunner FindRunner() {
//    var runners = NetworkRunner.GetInstancesEnumerator();

//    while (runners.MoveNext()) {
//      var runner = runners.Current;
//      // Ignore inactive runners - might just be unused scene objects or other orphans.
//      if (!runner.IsRunning)
//        continue;

//      if (runner.SharedInstanceUnitySceneRoot) {
//        var scene = runner.SharedInstanceUnitySceneRoot.scene;
//        if (scene == gameObject.scene) {
//          return runner;
//        }
//      }
//    }
//    return null;
//  }
//}

