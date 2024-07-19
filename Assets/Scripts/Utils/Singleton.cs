using UnityEngine;

// https://stackoverflow.com/a/72482271/19635374
public class Singleton<T> : MonoBehaviour where T : MonoBehaviour {

    private static T _instance;
    public static T Instance {
        get {
            if (_instance) {
                return _instance;
            }

            _instance = FindAnyObjectByType<T>();
            return _instance;
        }
        protected set => _instance = value;
    }

    protected void Set(T newInstance, bool dontDestroy = true) {
        if (Instance && newInstance != Instance) {
            Debug.LogWarning($"Singleton<{newInstance.GetType().Name}> was set while another already exists!");
            if (!ReferenceEquals(Instance, newInstance)) {
                DestroyImmediate(newInstance);
            }
        } else {
            _instance = newInstance;
            if (dontDestroy) {
                DontDestroyOnLoad(newInstance);
            }
        }
    }

    protected void Release() {
        if (!Instance) {
            return;
        }

        Destroy(Instance.gameObject);
    }
}
