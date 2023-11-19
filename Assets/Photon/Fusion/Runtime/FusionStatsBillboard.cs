namespace Fusion {
  using UnityEngine;


  /// <summary>
  /// Companion component for <see cref="FusionStats"/>, which automatically faces this GameObject toward the supplied Camera. If Camera == null, will face towards Camera.main.
  /// </summary>
  [Fusion.ScriptHelp(BackColor = ScriptHeaderBackColor.Olive)]
  [ExecuteAlways]
  public class FusionStatsBillboard : Fusion.Behaviour {

    /// <summary>
    /// Force a particular camera to billboard this object toward. Leave null to use Camera.main.
    /// </summary>
    [InlineHelp]
    public Camera Camera;

    // Camera find is expensive, so do it once per update for ALL implementations
    static float _lastCameraFindTime;
    static Camera _currentCam;

    FusionStats _fusionStats;

    private void Awake() {
      _fusionStats = GetComponent<FusionStats>();
    }

    private void OnEnable() {
      UpdateLookAt();
    }

    private void OnDisable() {
      transform.localRotation = default;
    }

    Camera MainCamera {
      set {
        _currentCam = value;
      }
      get {

        var time = Time.time;
        // Only look for the camera once per Update.
        if (time == _lastCameraFindTime)
          return _currentCam;

        _lastCameraFindTime = time;
        var cam = Camera.main;
        _currentCam = cam;
        return cam;
      }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos() {
      LateUpdate();
    }
#endif

    private void LateUpdate() {
      UpdateLookAt();
    }

    public void UpdateLookAt() {

      // Save the CPU here if our FusionStats is in overlay. Billboarding does nothing.
      if (_fusionStats && _fusionStats.CanvasType == FusionStats.StatCanvasTypes.Overlay) {
        return;
      }

      var cam = Camera ? Camera : MainCamera;

      if (cam) {
        if (enabled) {

          //var armOffset = transform.position - cam.transform.position;
          //if (_canvasT == null) {
          //  _canvasT = GetComponentInChildren<Canvas>()?.transform;
          //  if (_canvasT) {
          //    _canvasT.localPosition = Offset;
          //  }
          //} else {
          //  _canvasT.localPosition = Offset;
          //}

          transform.rotation = cam.transform.rotation;
          //transform.LookAt(transform.position + armOffset, cam.transform.up);
        }
      }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() {
      _currentCam = default;
      _lastCameraFindTime = default;
    }
  }
}