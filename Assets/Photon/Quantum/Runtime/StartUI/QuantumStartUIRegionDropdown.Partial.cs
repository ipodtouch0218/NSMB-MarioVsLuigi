namespace Quantum {
  using Photon.Realtime;
  using System.Collections.Generic;
  using System.Linq;
  using System.Threading.Tasks;

  public partial class QuantumStartUIRegionDropdown {
    partial void GetAvailableRegionsTaskUser(ref Task<List<string>> regionListPromise) {
      var client = new RealtimeClient();
      var appSettings = PhotonServerSettings.Global.AppSettings;
      if (string.IsNullOrEmpty(appSettings.AppIdQuantum)) {
        throw new System.Exception("AppId is not set in PhotonServerSettings.");
      }

      var regionHandler = client.ConnectToNameserverAndWaitForRegionsAsync(appSettings, pingRegions: false);
      regionListPromise = regionHandler.ContinueWith(x => x.Result.EnabledRegions.Select(region => region.Code).ToList(), 
        AsyncConfig.Global.TaskScheduler);
    }
  }
}