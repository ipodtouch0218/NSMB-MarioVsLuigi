namespace Quantum {
  using System;
  using Photon.Realtime;
  using UnityEngine;

  /// <summary>
  /// This class wraps the AppSettings into a scriptable object and adds a few Quantum connection related configurations.
  /// Connect to specific region cloud: UseNameSever = true,  FixedRegion = "us", Server = ""
  /// Connect to best region:           UseNameSever = true,  FixedRegion = "",   Server = ""
  /// Connect to (local) master server: UseNameSever = false, FixedRegion = "",   Server = "10.0.0.0.", Port = 5055
  /// Connect to (local) name server:   UseNameSever = true,  FixedRegion = "",   Server = "10.0.0.0.", Port = 5058
  /// </summary>
  [Serializable]
  [CreateAssetMenu(menuName = "Quantum/Configurations/PhotonServerSettings", order = EditorDefines.AssetMenuPriorityConfigurations + 1)]
  [QuantumGlobalScriptableObject(DefaultPath)]
  public class PhotonServerSettings : QuantumGlobalScriptableObject<PhotonServerSettings> {
    /// <summary>
    /// The default asset path to search or create a default server settings asset.
    /// </summary>
    public const string DefaultPath = "Assets/QuantumUser/Resources/PhotonServerSettings.asset";

    /// <summary>
    /// Photon AppSettings is serialized on this class.
    /// </summary>
    [Header("Photon App Settings")]
    public AppSettings AppSettings;
    /// <summary>
    /// PlayerTTL used when opening rooms.
    /// </summary>
    [Header("Photon Room Settings")]
    [InlineHelp]
    public int PlayerTtlInSeconds = 0;
    /// <summary>
    /// EmptyRoomTTL used when opening rooms.
    /// </summary>
    [InlineHelp]
    public int EmptyRoomTtlInSeconds = 0;
    /// <summary>
    /// Enable CRC for Photon networking to detect corrupted packages.
    /// Building the checksum with <see cref="Photon.Client.PhotonPeer.CrcEnabled"/> has a low processing overhead but increases integrity of sent and received data.
    /// This option could reduce client disconnects based on input corruption errors for example.
    /// </summary>
    [InlineHelp]
    public bool EnableCrc;
    /// <summary>
    /// Best region summary key used to store ping results in PlayerPrefs.
    /// </summary>
    [Header("Other Settings")]
    [InlineHelp]
    public string BestRegionSummaryKey = "Quantum.BestRegionSummary";

    /// <summary>
    /// Rejoining rooms (after the 10 second default timeout) is only possible when PlayerTTL > 0.
    /// </summary>
    public bool CanRejoin => PlayerTtlInSeconds > 0;

    /// <summary>
    /// Access best region summary in PlayerPrefs.
    /// </summary>
    public string BestRegionSummary {
      get => string.IsNullOrEmpty(BestRegionSummaryKey) ? string.Empty : PlayerPrefs.GetString(BestRegionSummaryKey);
      set => PlayerPrefs.SetString(BestRegionSummaryKey, value);
    }

    private void Reset() {
      // This makes sure that inlined default values are used (e.g. UseNameServer=true)
      AppSettings = new AppSettings();
    }

    /// <summary>
    /// Obsolete: use new AppSettings(appsSettings)
    /// </summary>
    [Obsolete("Use new AppSettings(appsSettings) instead")]
    public static AppSettings CloneAppSettings(AppSettings appSettings) {
      return new AppSettings(appSettings);
    }
  }
}