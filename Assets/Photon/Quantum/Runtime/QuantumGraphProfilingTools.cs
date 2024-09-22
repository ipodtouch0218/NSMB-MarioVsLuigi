namespace Quantum.Profiling {
  using System.Collections.Generic;
  using Photon.Client;
  using UnityEngine;
  using UnityEngine.UI;

  /// <summary>
  /// A Unity script that renders additional profiling tools to the graph UI.
    /// </summary>
  public sealed class QuantumGraphProfilingTools : MonoBehaviour {
    [SerializeField]
    private bool _enableOnAwake;
    [SerializeField]
    private GameObject _renderObject;
    [SerializeField]
    private Image _toggleVisibility;
    [SerializeField]
    private List<int> _fpsValues = new List<int>() { 0, 30, 60, 90, 120 };
    [SerializeField]
    private List<int> _lagValues = new List<int>() { 0, 30, 60, 90, 120, 150 };
    [SerializeField]
    private List<int> _jitterValues = new List<int>() { 0, 10, 20, 30, 40, 50 };
    [SerializeField]
    private List<int> _lossValues = new List<int>() { 0, 1, 2, 3, 5 };
    [SerializeField]
    private Text _fps;
    [SerializeField]
    private Text _incomingLag;
    [SerializeField]
    private Text _outgoingLag;
    [SerializeField]
    private Text _incomingJitter;
    [SerializeField]
    private Text _outgoingJitter;
    [SerializeField]
    private Text _incomingLoss;
    [SerializeField]
    private Text _outgoingLoss;

    private Color _defaultToggleColor;
    private int _lagIn;
    private int _lagOut;
    private int _jitterIn;
    private int _jitterOut;
    private int _lossIn;
    private int _lossOut;
    private bool _isSimulationEnabled;
    private PhotonPeer _peer;

    /// <summary>
    /// Toggle the visibility of the UI.
    /// </summary>
    public void ToggleVisibility() {
      _renderObject.SetActive(_renderObject.activeSelf == false);

      Refresh();
    }

    /// <summary>
    /// Toggle the target frame rate.
    /// </summary>
    public void ToggleFPS() {
      int frameRate = Application.targetFrameRate;
      if (frameRate < 0 || frameRate >= 9999) {
        frameRate = 0;
      }

      frameRate = _fpsValues[(_fpsValues.IndexOf(frameRate) + 1) % _fpsValues.Count];

      Application.targetFrameRate = frameRate;

      Refresh();
    }

    /// <summary>
    /// Toggle simulating incoming lag.
    /// </summary>
    public void ToggleIncomingLag() {
      _lagIn = _lagValues[(_lagValues.IndexOf(_lagIn) + 1) % _lagValues.Count];

      Refresh();
    }

    /// <summary>
    /// Toggle simulating outgoing lag.
    /// </summary>
    public void ToggleOutgoingLag() {
      _lagOut = _lagValues[(_lagValues.IndexOf(_lagOut) + 1) % _lagValues.Count];

      Refresh();
    }

    /// <summary>
    /// Toggle simulating incoming network jitter.
    /// </summary>
    public void ToggleIncomingJitter() {
      _jitterIn = _jitterValues[(_jitterValues.IndexOf(_jitterIn) + 1) % _jitterValues.Count];

      Refresh();
    }

    /// <summary>
    /// Toggle simulating outgoing network jitter.
    /// </summary>
    public void ToggleOutgoingJitter() {
      _jitterOut = _jitterValues[(_jitterValues.IndexOf(_jitterOut) + 1) % _jitterValues.Count];

      Refresh();
    }

    /// <summary>
    /// Toggle simulating incoming packet loss.
    /// </summary>
    public void ToggleIncomingLoss() {
      _lossIn = _lossValues[(_lossValues.IndexOf(_lossIn) + 1) % _lossValues.Count];

      Refresh();
    }

    /// <summary>
    /// Toggle simulating outgoing packet loss.
    /// </summary>
    public void ToggleOutgoingLoss() {
      _lossOut = _lossValues[(_lossValues.IndexOf(_lossOut) + 1) % _lossValues.Count];

      Refresh();
    }

    private void Awake() {
      _defaultToggleColor = _toggleVisibility.color;

      _renderObject.SetActive(_enableOnAwake);

      Refresh();
    }

    private void Update() {
      _peer = QuantumGraphProfilersUtility.GetNetworkPeer();

      if (_peer != null) {
        NetworkSimulationSet set = _peer.NetworkSimulationSettings;
        if (_peer.IsSimulationEnabled != _isSimulationEnabled || set.IncomingLag != _lagIn || set.OutgoingLag != _lagOut || set.IncomingJitter != _jitterIn || set.OutgoingJitter != _jitterOut || set.IncomingLossPercentage != _lossIn || set.OutgoingLossPercentage != _lossOut) {
          Refresh();
        }
      } else {
        if (_isSimulationEnabled == true || _lagIn != 0 || _lagOut != 0 || _jitterIn != 0 || _jitterOut != 0 || _lossIn != 0 || _lossOut != 0) {
          _isSimulationEnabled = false;

          _lagIn = 0;
          _lagOut = 0;
          _jitterIn = 0;
          _jitterOut = 0;
          _lossIn = 0;
          _lossOut = 0;

          Refresh();
        }
      }
    }

    private void Refresh() {
      _isSimulationEnabled = _lagIn > 0 || _lagOut > 0 || _jitterIn > 0 || _jitterOut > 0 || _lossIn > 0 || _lossOut > 0;

      if (_peer != null) {
        NetworkSimulationSet set = _peer.NetworkSimulationSettings;

        set.IncomingLag = _lagIn;
        set.OutgoingLag = _lagOut;
        set.IncomingJitter = _jitterIn;
        set.OutgoingJitter = _jitterOut;
        set.IncomingLossPercentage = _lossIn;
        set.OutgoingLossPercentage = _lossOut;

        _peer.IsSimulationEnabled = _isSimulationEnabled;
      }

      int frameRate = Application.targetFrameRate;
      if (frameRate <= 0 || frameRate >= 9999) {
        _fps.text = "∞";
      } else {
        _fps.text = frameRate.ToString();
      }

      _incomingLag.text = _lagIn.ToString();
      _outgoingLag.text = _lagOut.ToString();
      _incomingJitter.text = _jitterIn.ToString();
      _outgoingJitter.text = _jitterOut.ToString();
      _incomingLoss.text = _lossIn.ToString();
      _outgoingLoss.text = _lossOut.ToString();

      _toggleVisibility.color = _isSimulationEnabled == true ? Color.red : _defaultToggleColor;
    }
  }
}