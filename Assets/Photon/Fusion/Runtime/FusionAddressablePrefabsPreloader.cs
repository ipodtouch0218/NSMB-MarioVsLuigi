namespace Fusion {
  using System.Collections.Generic;
  using UnityEngine;

#if FUSION_ENABLE_ADDRESSABLES && !FUSION_DISABLE_ADDRESSABLES
  using UnityEngine.AddressableAssets;
  using UnityEngine.ResourceManagement.AsyncOperations;
#endif

  public class FusionAddressablePrefabsPreloader : MonoBehaviour {
#if FUSION_ENABLE_ADDRESSABLES && !FUSION_DISABLE_ADDRESSABLES
    private List<AsyncOperationHandle<GameObject>> _handles = new List<AsyncOperationHandle<GameObject>>();

    private async System.Threading.Tasks.Task Start() {
      var config = NetworkProjectConfig.Global;

      // there are a few ways to load an asset with Addressables (by label, by IResourceLocation, by address etc.)
      // but it seems that they're not fully interchangeable, i.e. loading by label will not make loading by address
      // be reported as done immediately; hence the only way to preload an asset for Quantum is to replicate
      // what it does internally, i.e. load with the very same parameters

      foreach (var (id, source) in config.PrefabTable.GetEntries()) {
        if (source is NetworkPrefabSourceAddressable addressable) {
          // we can't just LoadAssetAsync() because source does it, too:
          // https://forum.unity.com/threads/1-15-1-assetreference-not-allow-loadassetasync-twice.959910/
          var key = addressable.Address.RuntimeKey;
          var handle = Addressables.LoadAssetAsync<GameObject>(key);
          await handle.Task;
          _handles.Add(handle);
        }
      }
    }

    private void OnDestroy() {
      foreach (var handle in _handles) {
        Addressables.Release(handle);
      }
    }
#endif
  }
}
