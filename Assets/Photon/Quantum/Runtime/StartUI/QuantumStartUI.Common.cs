// merged StartUI

#region QuantumStartUIConnectionBase.cs

namespace Quantum {
  using System;
  using UnityEngine;

  /// <summary>
  /// The base class to be implemented by the SDK to provide connection and game starting/shutdown functionality.
  /// </summary>
  public abstract class QuantumStartUIConnectionBase : QuantumMonoBehaviour {
    /// <summary>
    /// The start parameters.
    /// </summary>
    public class StartParameter {
      /// <summary>
      /// The online start mode.
      /// </summary>
      public bool IsOnline;
      /// <summary>
      /// Should the room to be created be visible to the public matchmaking or not. 
      /// </summary>
      public bool IsVisible;
      /// <summary>
      /// The explicit region to connect to, set to <see langword="null"/> to use the Best Region mode.
      /// </summary>
      public string Region;
      /// <summary>
      /// The player name.
      /// </summary>
      public string PlayerName;
      /// <summary>
      /// The explicit room name to connect to or create. When null or empty random matchmaking will be used.
      /// </summary>
      public string RoomName;
      /// <summary>
      /// Optionally the selected character name.
      /// </summary>
      public string CharacterName;
      /// <summary>
      /// A callback that is executed when the connection or game fails during runtime.
      /// </summary>
      public Action<string> OnConnectionError;
    }

    /// <summary>
    /// The machine id scriptable object used to generate a unique AppVersion for the Photon matchmaking.
    /// This should be used most of the time during development as it uses an id that is unique to the machine and builds so non-compatible others
    /// clients do not matchmake with each other.
    /// </summary>
    [Header("App Version")]
    [InlineHelp, SerializeField] protected QuantumMachineId AppVersionMachineId;
    /// <summary>
    /// Append this to the <see cref="AppVersionMachineId"/> to isolate players from different maps inside the matchmaking.
    /// </summary>
    //[DrawIf("AppVersionMachineId", true)]
    [InlineHelp, SerializeField] public string AppVersionMachineIdPostfix;
    /// <summary>
    /// Set an explicit AppVersion to use instead of <see cref="AppVersionMachineId"/>.
    /// </summary>
    //[DrawIf("AppVersionMachineId", false)]
    [InlineHelp, SerializeField] protected string AppVersionOverride;
    /// <summary>
    /// Enabled Unity Multiplayer Play Mode to launch all clients simultaneously.
    /// </summary>
#if !QUANTUM_ENABLE_MPPM || !UNITY_EDITOR
    [HideInInspector]
#endif
    [Header("Multiplayer Play Mode")]
    [InlineHelp, SerializeField] protected bool EnableMultiplayerPlayMode = true;

    /// <summary>
    /// Implemented by the SDK to show the actual room name connected to.
    /// </summary>
    public abstract string RoomName { get; }
    /// <summary>
    /// Implemented by the SDK to show the actual region connected to.
    /// </summary>
    public abstract string Region { get; }
    /// <summary>
    /// Implemented by the SDK to show the current ping.
    /// </summary>
    public abstract int Ping { get; }

    /// <summary>
    /// Return the app version that has been configured by <see cref="AppVersionMachineId"/> and <see cref="AppVersionOverride"/>.
    /// Will append <see cref="AppVersionMachineIdPostfix"/> when machine id is selected.
    /// </summary>
    public string AppVersion => AppVersionMachineId != null ? $"{AppVersionMachineId.AppVersion}{AppVersionMachineIdPostfix}" : AppVersionOverride;
    
    /// <summary>
    /// Implement in the SDK to connect and start a game with the given start parameters.
    /// This method used exceptions to escalate errors.
    /// </summary>
    /// <param name="startParameter">Game connection and start parameters.</param>
    /// <returns>When the game has been started or failed</returns>
    public abstract System.Threading.Tasks.Task ConnectAsync(StartParameter startParameter);
    /// <summary>
    /// Implement in the SDK to disconnect from the current game and shutdown the connection.
    /// This method used exceptions to escalate errors.
    /// </summary>
    /// <returns>When the connection and game have been terminated</returns>
    public abstract System.Threading.Tasks.Task DisconnectAsync();
  }
}

#endregion


#region QuantumStartUIMppmCommand.cs

namespace Quantum {
  using static UnityEngine.Object;
  using static QuantumUnityExtensions;

  /// <summary>
  /// The Quantum Multiplayer Play Mode command to join a session with the mini menu.
  /// </summary>
  public class QuantumStartUIMppmCommand : QuantumMppmCommand {
    /// <summary>
    /// Find the mini menu object and execute the command on it.
    /// </summary>
    public override System.Threading.Tasks.Task ExecuteAsync() {
      var menu = FindAnyObjectByType<QuantumStartUI>();
      if (menu == null) {
        return System.Threading.Tasks.Task.CompletedTask;
      }

      return menu.TryExecuteMppmCommand(this);
    }
  }

  /// <summary>
  /// Ask the menu to start.
  /// </summary>
  public class QuantumStartUIMppmConnectCommand : QuantumStartUIMppmCommand {
    /// <summary>
    /// The region to connect to.
    /// </summary>
    public string Region;
    /// <summary>
    /// The session/room name to join.
    /// </summary>
    public string RoomName;
  }

  /// <summary>
  /// Ask the menu to stop.
  /// </summary>
  public class QuantumStartUIMppmDisconnectCommand : QuantumStartUIMppmCommand {
  }
}

#endregion

