namespace Fusion {
  using System;
  using System.Collections.Generic;
  using UnityEngine;
  using UnityEngine.SceneManagement;
  using UnityEngine.Serialization;

  public class NetworkObjectProviderDefault : Fusion.Behaviour, INetworkObjectProvider {
    /// <summary>
    /// If enabled, the provider will delay acquiring a prefab instance if the scene manager is busy.
    /// </summary>
    [InlineHelp]
    public bool DelayIfSceneManagerIsBusy = true;

    public virtual NetworkObjectAcquireResult AcquirePrefabInstance(NetworkRunner runner, in NetworkPrefabAcquireContext context, out NetworkObject instance) {

      instance = null;

      if (DelayIfSceneManagerIsBusy && runner.SceneManager.IsBusy) {
        return NetworkObjectAcquireResult.Retry;
      }

      NetworkObject prefab;
      try {
        prefab = runner.Prefabs.Load(context.PrefabId, isSynchronous: context.IsSynchronous);
      } catch (Exception ex) {
        Log.Error($"Failed to load prefab: {ex}");
        return NetworkObjectAcquireResult.Failed;
      }

      if (!prefab) {
        // this is ok, as long as Fusion does not require the prefab to be loaded immediately;
        // if an instance for this prefab is still needed, this method will be called again next update
        return context.IsSynchronous ? NetworkObjectAcquireResult.Failed : NetworkObjectAcquireResult.Retry;
      }

      instance = InstantiatePrefab(runner, prefab);
      Assert.Check(instance);

      if (context.DontDestroyOnLoad) {
        runner.MakeDontDestroyOnLoad(instance.gameObject);
      } else {
        runner.MoveToRunnerScene(instance.gameObject);
      }

      runner.Prefabs.AddInstance(context.PrefabId);
      return NetworkObjectAcquireResult.Success;
    }

    public virtual void ReleaseInstance(NetworkRunner runner, in NetworkObjectReleaseContext context) {
      var instance = context.Object;

      if (!context.IsBeingDestroyed) {
        if (context.TypeId.IsPrefab) {
          DestroyPrefabInstance(runner, context.TypeId.AsPrefabId, instance);
        } else if (context.TypeId.IsSceneObject) {
          DestroySceneObject(runner, context.TypeId.AsSceneObjectId, instance);
        } else if (context.IsNestedObject) {
          DestroyPrefabNestedObject(runner, instance);
        } else {
          throw new NotImplementedException($"Unknown type id {context.TypeId}");
        }
      }

      if (context.TypeId.IsPrefab) {
        runner.Prefabs.RemoveInstance(context.TypeId.AsPrefabId);
      }
    }



    protected virtual NetworkObject InstantiatePrefab(NetworkRunner runner, NetworkObject prefab) {
      return Instantiate(prefab);
    }

    protected virtual void DestroyPrefabInstance(NetworkRunner runner, NetworkPrefabId prefabId, NetworkObject instance) {
      Destroy(instance.gameObject);
    }
    
    protected virtual void DestroyPrefabNestedObject(NetworkRunner runner, NetworkObject instance) {
      Destroy(instance.gameObject);
    }

    protected virtual void DestroySceneObject(NetworkRunner runner, NetworkSceneObjectId sceneObjectId, NetworkObject instance) {
      Destroy(instance.gameObject);
    }
  }
}