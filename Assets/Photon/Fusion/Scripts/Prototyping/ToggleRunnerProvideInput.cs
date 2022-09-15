using UnityEngine;
using Fusion;

#if UNITY_EDITOR
using Fusion.Editor;
#endif

[DisallowMultipleComponent]
[AddComponentMenu("Fusion/Prototyping/Toggle Runner Provide Input")]
[ScriptHelp(BackColor = EditorHeaderBackColor.Steel)]
public class ToggleRunnerProvideInput : Fusion.Behaviour {

  private static ToggleRunnerProvideInput _instance;

  public void Awake() {

    if (NetworkProjectConfig.Global.PeerMode != NetworkProjectConfig.PeerModes.Multiple) {
      Debug.LogWarning($"{nameof(ToggleRunnerProvideInput)} only works in Multi-Peer mode. Destroying.");
      Destroy(this);
      return;
    }

    // Enforce singleton across all Runners.
    if (_instance)
      Destroy(this);
    _instance = this;
  }

  public void Update() {
    if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.LeftCommand)) && Input.GetKey(KeyCode.LeftShift)) {
      if (Input.GetKeyDown(KeyCode.Alpha0))
        ToggleAll(-1);
      else if (Input.GetKeyDown(KeyCode.Alpha1))
        ToggleAll(0);
      else if (Input.GetKeyDown(KeyCode.Alpha2))
        ToggleAll(1);
      else if (Input.GetKeyDown(KeyCode.Alpha3))
        ToggleAll(2);
      else if (Input.GetKeyDown(KeyCode.Alpha4))
        ToggleAll(3);
      else if (Input.GetKeyDown(KeyCode.Alpha5))
        ToggleAll(4);
      else if (Input.GetKeyDown(KeyCode.Alpha6))
        ToggleAll(5);
      else if (Input.GetKeyDown(KeyCode.Alpha7))
        ToggleAll(6);
      else if (Input.GetKeyDown(KeyCode.Alpha8))
        ToggleAll(7);
      else if (Input.GetKeyDown(KeyCode.Alpha9))
        ToggleAll(8);
    }
  }

  private void ToggleAll(int runnerIndex) {

    var runners = NetworkRunner.GetInstancesEnumerator();

    int index = 0;
    while (runners.MoveNext()) {

      var runner = runners.Current;

      // Ignore inactive runners - might just be unused scene objects or other orphans.
      if (runner == null || !runner.IsRunning)
        continue;

      bool enable = runnerIndex == -1 || index == runnerIndex;
      runner.ProvideInput = enable;
      index++;
    }
#if UNITY_EDITOR
    // If we have a RunnerVisiblityControlWindow open, it needs to know to refresh.
    if (RunnerVisibilityControlsWindow.Instance) {
      RunnerVisibilityControlsWindow.Instance.Repaint();
    }
#endif
  }
}


