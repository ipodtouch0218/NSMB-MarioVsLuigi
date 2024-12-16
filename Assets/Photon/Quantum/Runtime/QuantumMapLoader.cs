namespace Quantum {
  using Photon.Analyzer;
  using UnityEngine;
  using static QuantumUnityExtensions;

  /// <summary>
  /// A Unity script that helps with loading Quantum maps and scenes.
  /// </summary>
  public class QuantumMapLoader : QuantumMonoBehaviour {
    [StaticField]
    private static QuantumMapLoader _instance;

    [StaticField]
    private static bool _isApplicationQuitting;

    /// <summary>
    /// Gets or creates a global instance of the QuantumMapLoader.
    /// <para>Will return null when the application is about to quit.</para>
    /// </summary>
    public static QuantumMapLoader Instance {
      get {
        if (_isApplicationQuitting) {
          return null;
        }

        if (_instance == null) {
          _instance = FindFirstObjectByType<QuantumMapLoader>();
        }

        if (_instance == null) {
          _instance = new GameObject("QuantumMapLoader").AddComponent<QuantumMapLoader>();
        }

        return _instance;
      }
    }

    /// <summary>
    /// Unity Awake method. Tag this GameObject as DontDestroyOnLoad.
    /// </summary>
    public void Awake() {
      DontDestroyOnLoad(this);
    }

    /// <summary>
    /// Unity OnApplicationQuit callback, sets the application quitting flag to prevent further instance creation.
    /// </summary>
    public void OnApplicationQuit() {
      _isApplicationQuitting = true;
    }

    /// <summary>
    /// Reset global statics.
    /// </summary>
    [StaticFieldResetMethod]
    public static void ResetStatics() {
      _instance              = null;
      _isApplicationQuitting = false;
    }
  }
}