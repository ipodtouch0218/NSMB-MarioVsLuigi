using UnityEngine;

// https://stackoverflow.com/a/72482271/19635374
public class Singleton<T> : MonoBehaviour where T : MonoBehaviour {

    public static T Instance { get; protected set; }

    protected void Set(T newInstance, bool dontDestroy = true) {
        if (Instance) {
            Debug.LogWarning($"Singleton<{newInstance.GetType().Name}> was set while another already exists!");
            if (!ReferenceEquals(Instance, newInstance))
                DestroyImmediate(newInstance);
        } else {
            Instance = newInstance;
            if (dontDestroy)
                DontDestroyOnLoad(newInstance);
        }
    }

    protected void Release() {
        if (!Instance)
            return;

        Destroy(Instance.gameObject);
    }
}
