using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Singleton<T> : MonoBehaviour where T : MonoBehaviour {
    protected static T instance; 
    public static T Instance {
        get {
            return instance;
        }
    }
    protected bool InstanceCheck() {
        if (instance != null) {
            Destroy(gameObject);
            return false;
        }
        DontDestroyOnLoad(gameObject);
        return true;
    }
}