namespace Quantum {
  using System;
  using System.IO;
  using Photon.Realtime;
  using UnityEngine;
  using static QuantumUnityExtensions;

  /// <summary>
  /// The frame differ shows the frame dumps of all clients in a game and allows to compare them after a checksum error.
  /// This class renders the GUI on the screen and is usable in builds.
  /// </summary>
  public class QuantumFrameDiffer : QuantumMonoBehaviour {
    /// <summary>
    /// The state saves the frame dumps to be displayed.
    /// </summary>
    public QuantumFrameDifferGUI.FrameDifferState State = new QuantumFrameDifferGUI.FrameDifferState();

    QuantumFrameDifferGUI _gui;

    class QuantumFrameDifferGUIRuntime : QuantumFrameDifferGUI {
      public QuantumFrameDifferGUIRuntime(FrameDifferState state) : base(state) {
      }

      public override int TextLineHeight {
        get { return 20; }
      }

      public override Rect Position {
        get { return new Rect(0, 0, Screen.width, Screen.height); }
      }

      public override void DrawHeader() {
        GUILayout.Space(5);

        if (_hidden) {
          if (GUILayout.Button("Show Quantum Frame Differ", MiniButton, GUILayout.Height(16))) {
            _hidden = false;
          }
        } else {
          if (GUILayout.Button("Hide", MiniButton, GUILayout.Height(16))) {
            _hidden = true;
          }
          if (GUILayout.Button("Save", MiniButton, GUILayout.Height(16))) {
            var dateString = DateTime.Now.ToString("yyyy-MM-dd-hh-mm-ss");

#if UNITY_EDITOR
            dateString += "-Editor";
#endif

            var savePath = Path.Combine(Application.persistentDataPath, $"Diff_{dateString}.json");

            File.WriteAllText(savePath, JsonUtility.ToJson(State));
          }
        }
      }
    }

    void OnGUI() {
      if (_gui == null) {
        _gui = new QuantumFrameDifferGUIRuntime(State);
      }

      GUILayout.BeginArea(_gui.Position);

      _gui.OnGUI();

      GUILayout.EndArea();
    }

    /// <summary>
    /// Find and or create a new <see cref="QuantumFrameDiffer"/> component and show the GUI.
    /// </summary>
    /// <returns>The frame differ component.</returns>
    public static QuantumFrameDiffer Show() {
      var instance = FindFirstObjectByType<QuantumFrameDiffer>();
      if (instance) {
        instance._gui.Show();
        return instance;
      }

      GameObject gameObject;
      gameObject = new GameObject(typeof(QuantumFrameDiffer).Name);

      var differ = gameObject.AddComponent<QuantumFrameDiffer>();
      if (differ._gui == null) {
        differ._gui = new QuantumFrameDifferGUIRuntime(differ.State);
      }

      differ._gui.Show();

      return differ;
    }

    /// <summary>
    /// A helper method to try to get the Photon nickname of a player using its Photon actor id.
    /// </summary>
    /// <param name="client">Client connection object</param>
    /// <param name="actorId">Photon actor id</param>
    /// <returns>A nickname or null</returns>
    public static string TryGetPhotonNickname(RealtimeClient client, int actorId) {
      // Try to get Photon nickname
      if (client != null && client.CurrentRoom != null) {
        client.CurrentRoom.Players.TryGetValue(actorId, out var player);
        if (player != null) {
          return player.NickName;
        }
      }

      return null;
    }
  }
}